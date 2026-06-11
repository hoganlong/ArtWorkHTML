using Newtonsoft.Json.Linq;
using Npgsql;
using System.Globalization;
using System.Text;

namespace ArtWorkHTML;

public partial class ArtworkHTML
{
  private const string ShowsBaseUrl = "https://keithlong-art-photos.s3.us-east-1.amazonaws.com/";

  private sealed class ShowRow
  {
    public int Id;
    public string ShowName = "";
    public string Gallery = "";
    public string Address = "";
    public DateTime? StartDt;
    public DateTime? EndDt;
    public int? Year;
    public string ShowType = "";
    public string Reception = "";
    public string CuratedBy = "";
    public string SponsoredBy = "";
  }

  private sealed class ArtlistItemRow
  {
    public int Id;
    public int ArtlistId;
    public int? Sequence;
    public int? ArtworkId;
    public long? ArtworkImageId;
    public int? PhotoId;
    public string ItemType = "";
    public string Url = "";
    public string Notes = "";
  }

  private sealed class ArtworkLookup
  {
    public string FileName = "";
    public string Title = "";
    public string HumanId = "";
    public DateTime CreateDt;
    public string Medium = "";
    public string Dimensions = "";
    public string FoldedDimensions = "";
    public string Notes = "";
    public bool Unsigned;
  }

  private sealed class ArtworkImageLookup
  {
    public string Url = "";
    public string View = "";
  }

  private sealed class PhotoLookup
  {
    public string FileLocation = "";
    public string Description = "";
  }

  // Display labels for the collapsible sections, in the order they should appear.
  private static readonly (string ItemType, string Heading)[] CollapsibleSections = new[]
  {
    ("ART_IN_SHOW",       "Art in Show"),
    ("SHOW_INSTALLATION", "Exhibition photos"),
    ("SHOW_OPENING",      "Opening"),
    ("SHOW_RECEPTION",    "Reception"),
    ("SHOW_LAYOUT",       "Layout"),
  };

  private async Task GenerateShowsPage()
  {
    var shows = await LoadShowsAsync();
    var imageCounts = await LoadShowImageCountsAsync();
    var showsDir = Path.Combine(_outputDirectory, "shows");
    Directory.CreateDirectory(showsDir);

    await WriteShowsIndex(shows, imageCounts);

    foreach (var show in shows)
      await WriteShowPage(show, showsDir);

    Console.WriteLine($"  ✓ shows.html ({shows.Count} shows)");
  }

  // Per-show count of renderable images: skips SEQ_ITEM (never rendered) and
  // items with no image source at all (would also be skipped at render time).
  private async Task<Dictionary<int, int>> LoadShowImageCountsAsync()
  {
    var dict = new Dictionary<int, int>();
    const string sql = @"
      SELECT al.show_id,
             COUNT(*) AS image_count
      FROM artlist al
      JOIN artlist_item li ON li.artlist_id = al.id_field
      WHERE al.show_id IS NOT NULL
        AND li.item_type IS DISTINCT FROM 'SEQ_ITEM'
        AND (li.artwork_id IS NOT NULL
             OR li.artwork_image_id IS NOT NULL
             OR li.photo_id IS NOT NULL
             OR (li.url IS NOT NULL AND li.url <> ''))
      GROUP BY al.show_id";

    await using var conn = new NpgsqlConnection(_connectionString);
    await conn.OpenAsync();
    await using var cmd = new NpgsqlCommand(sql, conn);
    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
      dict[reader.GetInt32(0)] = Convert.ToInt32(reader.GetValue(1));
    return dict;
  }

  private async Task<List<ShowRow>> LoadShowsAsync()
  {
    // Sort by a unified "show year": prefer the actual start date's year, fall
    // back to the explicit YEAR field for shows where only a year is known.
    const string sql = @"
      SELECT id_field, show_name, gallery, address, show_start_dt, show_end_dt,
             show_type, reception, curated_by, sponsored_by, year
      FROM show
      ORDER BY COALESCE(EXTRACT(YEAR FROM show_start_dt)::int, year) DESC NULLS LAST,
               show_start_dt DESC NULLS LAST,
               id_field DESC";

    var shows = new List<ShowRow>();
    await using var conn = new NpgsqlConnection(_connectionString);
    await conn.OpenAsync();
    await using var cmd = new NpgsqlCommand(sql, conn);
    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
      shows.Add(new ShowRow
      {
        Id           = reader.GetInt32(0),
        ShowName     = reader.IsDBNull(1) ? "" : reader.GetString(1),
        Gallery      = reader.IsDBNull(2) ? "" : reader.GetString(2),
        Address      = reader.IsDBNull(3) ? "" : reader.GetString(3),
        StartDt      = reader.IsDBNull(4) ? null : reader.GetDateTime(4),
        EndDt        = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
        ShowType     = reader.IsDBNull(6) ? "" : reader.GetString(6),
        Reception    = reader.IsDBNull(7) ? "" : reader.GetString(7),
        CuratedBy    = reader.IsDBNull(8) ? "" : reader.GetString(8),
        SponsoredBy  = reader.IsDBNull(9) ? "" : reader.GetString(9),
        Year         = reader.IsDBNull(10) ? null : Convert.ToInt32(reader.GetValue(10)),
      });
    }
    return shows;
  }

  private async Task WriteShowsIndex(List<ShowRow> shows, Dictionary<int, int> imageCounts)
  {
    var html = new StringBuilder();
    html.AppendLine(GetHtmlHeader("Shows - Keith Long Archive"));
    html.AppendLine(@"
    <div class='container'>
        <h1>Shows</h1>
        <p class='subtitle'><a href='index.html'>← Back to Home</a></p>");

    if (shows.Count == 0)
    {
      html.AppendLine("        <p>No shows recorded.</p>");
    }
    else
    {
      // SOLO under "Solo"; everything else (GROUP, 2PERSON, blank) under "Group".
      var solo  = shows.Where(s => string.Equals(s.ShowType, "SOLO", StringComparison.OrdinalIgnoreCase)).ToList();
      var group = shows.Where(s => !string.Equals(s.ShowType, "SOLO", StringComparison.OrdinalIgnoreCase)).ToList();
      AppendShowSection(html, "Solo",  solo,  imageCounts);
      AppendShowSection(html, "Group", group, imageCounts);
    }

    html.AppendLine("    </div>");
    html.AppendLine(GetHtmlFooter());
    await File.WriteAllTextAsync(Path.Combine(_outputDirectory, "shows.html"), html.ToString());
  }

  private void AppendShowSection(StringBuilder html, string heading, List<ShowRow> shows, Dictionary<int, int> imageCounts)
  {
    if (shows.Count == 0) return;
    html.AppendLine($"        <h2 class='show-section-heading'>{EscapeHtml(heading)}</h2>");

    int? currentYear = null;
    bool firstGroup = true;
    bool inList = false;
    foreach (var show in shows)
    {
      var showYear = GetShowYear(show);
      if (showYear != currentYear)
      {
        if (inList) html.AppendLine("        </ul>");
        var yearHeading = showYear.HasValue ? showYear.Value.ToString() : "Date unknown";
        var yearClass = firstGroup ? "show-year show-year-first" : "show-year";
        html.AppendLine($"        <h3 class='{yearClass}'>{yearHeading}</h3>");
        html.AppendLine("        <ul class='show-index'>");
        currentYear = showYear;
        inList = true;
        firstGroup = false;
      }

      var title = EscapeHtml(BuildShowTitle(show));
      var gallerySpan = string.IsNullOrWhiteSpace(show.Gallery)
        ? ""
        : $" <span class='show-index-gallery'>— {EscapeHtml(show.Gallery)}</span>";
      var count = imageCounts.TryGetValue(show.Id, out var c) ? c : 0;
      var countSpan = count > 0
        ? $" <span class='show-index-count'>— {count} image{(count == 1 ? "" : "s")}</span>"
        : "";
      html.AppendLine($"          <li><a href='shows/show-{show.Id}.html'>{title}</a>{gallerySpan}{countSpan}</li>");
    }
    if (inList) html.AppendLine("        </ul>");
  }

  // Year used for grouping the shows.html index: prefer the start date's year,
  // fall back to the explicit YEAR field, else null (renders under "Date unknown").
  private static int? GetShowYear(ShowRow show)
  {
    if (show.StartDt.HasValue) return show.StartDt.Value.Year;
    if (show.Year.HasValue) return show.Year.Value;
    return null;
  }

  private async Task WriteShowPage(ShowRow show, string showsDir)
  {
    var artlistIds = await LoadArtlistIdsForShow(show.Id);
    var items = artlistIds.Count == 0
      ? new List<ArtlistItemRow>()
      : await LoadArtlistItems(artlistIds);

    var artworkLookups = await LoadArtworkLookups(items);
    var artworkImageLookups = await LoadArtworkImageLookups(items);
    var photoLookups = await LoadPhotoLookups(items);

    var byType = items.GroupBy(i => i.ItemType ?? "")
                      .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

    const string pathPrefix = "../";
    var title = BuildShowTitle(show);

    var html = new StringBuilder();
    html.AppendLine(GetHtmlHeader($"{title} - Keith Long Archive", pathPrefix));
    html.AppendLine($@"
    <div class='container'>
        <h1>{EscapeHtml(title)}</h1>
        <p class='subtitle'><a href='{pathPrefix}shows.html'>← Back to Shows</a> &middot; <a href='{pathPrefix}index.html'>Home</a></p>");

    html.AppendLine(RenderShowMetadata(show));

    if (byType.TryGetValue("SHOW_INVITE_FRONT", out var invitesFront) && invitesFront.Count > 0
        || byType.TryGetValue("SHOW_INVITE_BACK", out var invitesBack) && invitesBack.Count > 0)
    {
      var invites = new List<ArtlistItemRow>();
      if (byType.TryGetValue("SHOW_INVITE_FRONT", out var f)) invites.AddRange(f);
      if (byType.TryGetValue("SHOW_INVITE_BACK", out var b)) invites.AddRange(b);
      html.AppendLine("        <h2>Invitation</h2>");
      html.AppendLine("        <div class='gallery show-invites'>");
      foreach (var item in invites.OrderBy(i => i.Sequence ?? int.MaxValue).ThenBy(i => i.Id))
        html.AppendLine(RenderItem(item, show.Id, artworkLookups, artworkImageLookups, photoLookups));
      html.AppendLine("        </div>");
    }

    foreach (var (itemType, heading) in CollapsibleSections)
    {
      if (!byType.TryGetValue(itemType, out var bucket) || bucket.Count == 0) continue;
      html.AppendLine($"        <details class='show-section'>");
      html.AppendLine($"          <summary>{EscapeHtml(heading)} ({bucket.Count})</summary>");
      html.AppendLine($"          <div class='gallery'>");
      foreach (var item in bucket.OrderBy(i => i.Sequence ?? int.MaxValue).ThenBy(i => i.Id))
        html.AppendLine(RenderItem(item, show.Id, artworkLookups, artworkImageLookups, photoLookups));
      html.AppendLine($"          </div>");
      html.AppendLine($"        </details>");
    }

    html.AppendLine("    </div>");
    html.AppendLine(GetLightboxHtml());
    html.AppendLine(GetLightboxScriptTag(pathPrefix));
    html.AppendLine(GetHtmlFooter(pathPrefix));

    await File.WriteAllTextAsync(Path.Combine(showsDir, $"show-{show.Id}.html"), html.ToString());
  }

  private string RenderShowMetadata(ShowRow show)
  {
    var sb = new StringBuilder();
    sb.AppendLine("        <dl class='show-meta'>");
    void Row(string label, string value)
    {
      if (string.IsNullOrWhiteSpace(value)) return;
      sb.AppendLine($"          <dt>{label}</dt><dd>{EscapeHtml(value).Replace("\n", "<br/>")}</dd>");
    }
    Row("Gallery", show.Gallery);
    Row("Address", show.Address);
    var range = FormatDateRange(show.StartDt, show.EndDt);
    if (!string.IsNullOrEmpty(range))
      Row("Dates", range);
    else if (show.Year.HasValue)
      Row("Year", show.Year.Value.ToString());
    if (!string.IsNullOrEmpty(show.ShowType)) Row("Type", FormatShowType(show.ShowType));
    Row("Reception", show.Reception);
    Row("Curated by", show.CuratedBy);
    Row("Sponsored by", show.SponsoredBy);
    sb.AppendLine("        </dl>");
    return sb.ToString();
  }

  private string RenderItem(
    ArtlistItemRow item,
    int showId,
    Dictionary<int, ArtworkLookup> artworkLookups,
    Dictionary<long, ArtworkImageLookup> aiLookups,
    Dictionary<int, PhotoLookup> photoLookups)
  {
    if (!TryResolveItemImage(item, showId, artworkLookups, aiLookups, photoLookups, out var previewUrl, out var fullUrl, out var _))
      return "";

    var sb = new StringBuilder();
    sb.AppendLine("<div class='gallery-item tag-active'>");
    sb.AppendLine($"  <a href='{fullUrl}' rel='noopener noreferrer'><img src='{previewUrl}' loading='lazy' title='(click for full size)'/></a>");
    sb.AppendLine("  <div class='desc item-description'>");

    if (item.ArtworkId.HasValue && artworkLookups.TryGetValue(item.ArtworkId.Value, out var aw))
    {
      // Mirror the all-artwork-page metadata block, but without thumbnails, tags,
      // series badge, or [tif file] link.
      sb.AppendLine($"    {BlankOrWithBR(aw.Title, "  ")}");
      sb.AppendLine($"    {BlankOrWithBR(DateOrEmpty(aw.CreateDt), "  ")}");
      sb.AppendLine($"    {BlankOrWithBR(aw.Medium, "  ")}");
      sb.AppendLine($"    {BlankOrWithBR(aw.Dimensions, "  ", " inches")}");
      sb.AppendLine($"    {BlankOrWithBR(aw.FoldedDimensions, "   Folded: ", " inches")}");
      sb.AppendLine($"    {BlankOrWithBR(aw.Notes, "  Notes: ")}");
      if (aw.Unsigned)
        sb.AppendLine("    <span style='color:lightcoral;'>This artwork is unsigned</span><br/>");
    }
    else if (item.ArtworkImageId.HasValue && aiLookups.TryGetValue(item.ArtworkImageId.Value, out var ai)
             && !string.IsNullOrWhiteSpace(ai.View))
    {
      sb.AppendLine($"    {EscapeHtml(ai.View)}<br/>");
    }

    if (!string.IsNullOrWhiteSpace(item.Notes))
      sb.AppendLine($"    {EscapeHtml(item.Notes).Replace("\n", "<br/>")}<br/>");

    sb.AppendLine("  </div>");
    sb.AppendLine("</div>");
    return sb.ToString();
  }

  // Returns true if a valid image source could be resolved. Logs a console
  // warning and returns false when an item has no usable source.
  private bool TryResolveItemImage(
    ArtlistItemRow item,
    int showId,
    Dictionary<int, ArtworkLookup> artworkLookups,
    Dictionary<long, ArtworkImageLookup> aiLookups,
    Dictionary<int, PhotoLookup> photoLookups,
    out string previewUrl,
    out string fullUrl,
    out string captionTitle)
  {
    previewUrl = ""; fullUrl = ""; captionTitle = "";

    if (item.ArtworkId.HasValue && artworkLookups.TryGetValue(item.ArtworkId.Value, out var aw)
        && !string.IsNullOrEmpty(aw.FileName))
    {
      (previewUrl, fullUrl) = BuildJpgUrls(aw.FileName);
      captionTitle = string.IsNullOrEmpty(aw.Title) ? aw.HumanId : aw.Title;
      return true;
    }

    if (item.ArtworkImageId.HasValue)
    {
      var id = item.ArtworkImageId.Value;
      if (aiLookups.TryGetValue(id, out var ai) && !string.IsNullOrEmpty(ai.Url))
      {
        (previewUrl, fullUrl) = BuildJpgUrls(ai.Url);
        captionTitle = ai.View;
        return true;
      }
      previewUrl = string.Format(S3_ARTWORK_IMAGE_URL, id, "large");
      fullUrl    = string.Format(S3_ARTWORK_IMAGE_URL, id, "full");
      if (aiLookups.TryGetValue(id, out var aiMeta)) captionTitle = aiMeta.View;
      return true;
    }

    if (item.PhotoId.HasValue && photoLookups.TryGetValue(item.PhotoId.Value, out var ph)
        && !string.IsNullOrWhiteSpace(ph.FileLocation))
    {
      (previewUrl, fullUrl) = BuildJpgUrls(ph.FileLocation.Trim());
      captionTitle = ph.Description;
      return true;
    }

    if (!string.IsNullOrWhiteSpace(item.Url))
    {
      (previewUrl, fullUrl) = BuildJpgUrls(item.Url.Trim());
      return true;
    }

    Console.WriteLine($"  ! Show {showId}, artlist_item {item.Id}: no image source (artwork_id/artwork_image_id/photo_id/url all empty)");
    return false;
  }

  // Resolves an artlist_item file reference to (preview, full) S3 URLs.
  // Conventions:
  //   - sscan/ files always come in _small / _large / _full sized variants
  //     under sscan/jpg/. Strip any extension and any leading sscan/[jpg/]
  //     to find the basename, then build the _large and _full URLs.
  //   - scans/ files are single files under scans/jpg/<base>.jpg.
  //   - Otherwise: if the value already names a literal file (has .jpg/.jpeg
  //     /.png extension or contains a /jpg/ segment), use it literally; else
  //     fall back to the bare-name jpg/<name>.jpg convention.
  private static (string preview, string full) BuildJpgUrls(string fileName)
  {
    if (fileName.StartsWith("sscan/", StringComparison.OrdinalIgnoreCase))
    {
      var rest = fileName.Substring("sscan/".Length);
      if (rest.StartsWith("jpg/", StringComparison.OrdinalIgnoreCase))
        rest = rest.Substring("jpg/".Length);
      var basename = StripImageExtension(rest);
      // If the stored path already carried a sized suffix, drop it so we can
      // emit both _large (preview) and _full (lightbox) variants.
      foreach (var suffix in new[] { "_large", "_full", "_small" })
        if (basename.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
          basename = basename.Substring(0, basename.Length - suffix.Length);
      return (
        $"{ShowsBaseUrl}sscan/jpg/{basename}_large.jpg",
        $"{ShowsBaseUrl}sscan/jpg/{basename}_full.jpg");
    }
    if (fileName.StartsWith("scans/", StringComparison.OrdinalIgnoreCase))
    {
      var rest = fileName.Substring("scans/".Length);
      if (rest.StartsWith("jpg/", StringComparison.OrdinalIgnoreCase))
        rest = rest.Substring("jpg/".Length);
      var basename = StripImageExtension(rest);
      var url = $"{ShowsBaseUrl}scans/jpg/{basename}.jpg";
      return (url, url);
    }
    var ext = Path.GetExtension(fileName);
    if (ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
        || ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
        || ext.Equals(".png", StringComparison.OrdinalIgnoreCase))
    {
      var literal = $"{ShowsBaseUrl}{fileName}";
      return (literal, literal);
    }
    if (fileName.Contains("/jpg/", StringComparison.OrdinalIgnoreCase))
    {
      var url = $"{ShowsBaseUrl}{fileName}.jpg";
      return (url, url);
    }
    var flat = $"{ShowsBaseUrl}jpg/{fileName}.jpg";
    return (flat, flat);
  }

  private static string StripImageExtension(string s)
  {
    var ext = Path.GetExtension(s);
    if (ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
        || ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
        || ext.Equals(".png", StringComparison.OrdinalIgnoreCase))
      return s.Substring(0, s.Length - ext.Length);
    return s;
  }

  // Returns the set of bare basenames (no path, no extension, no _small/_large
  // /_full suffix) for every image that the shows feature claims OR that lives
  // in the photo table — used by GenerateArtworkPages to keep these out of
  // scans.html so they don't surface as unclassified scans.
  internal async Task<HashSet<string>> LoadShowImageBasenamesAsync()
  {
    var basenames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    await using var conn = new NpgsqlConnection(_connectionString);
    await conn.OpenAsync();

    // 1. Every non-empty artlist_item.url
    await using (var cmd = new NpgsqlCommand(
      "SELECT url FROM artlist_item WHERE url IS NOT NULL AND url <> ''", conn))
    await using (var reader = await cmd.ExecuteReaderAsync())
    {
      while (await reader.ReadAsync())
      {
        var bn = ExtractBasename(reader.GetString(0));
        if (!string.IsNullOrEmpty(bn)) basenames.Add(bn);
      }
    }

    // 2. Every artwork_image.url referenced by an artlist_item.artwork_image_id
    await using (var cmd = new NpgsqlCommand(
      @"SELECT ai.url
        FROM artwork_image ai
        JOIN artlist_item li ON li.artwork_image_id = ai.id_field
        WHERE ai.url IS NOT NULL AND ai.url <> ''", conn))
    await using (var reader = await cmd.ExecuteReaderAsync())
    {
      while (await reader.ReadAsync())
      {
        var bn = ExtractBasename(reader.GetString(0));
        if (!string.IsNullOrEmpty(bn)) basenames.Add(bn);
      }
    }

    // 3. Every photo.file_location — photos are tracked in their own table and
    //    should never be treated as unclassified scans, whether or not a show
    //    happens to reference them.
    await using (var cmd = new NpgsqlCommand(
      @"SELECT file_location
        FROM photo
        WHERE file_location IS NOT NULL AND file_location <> ''", conn))
    await using (var reader = await cmd.ExecuteReaderAsync())
    {
      while (await reader.ReadAsync())
      {
        var bn = ExtractBasename(reader.GetString(0));
        if (!string.IsNullOrEmpty(bn)) basenames.Add(bn);
      }
    }

    return basenames;
  }

  // Pulls a bare basename from any of the forms an artlist_item.url can take:
  // "sscan/jpg/KLSCAN_0065_large.jpg", "sscan/jpg/KLSCAN_0065", "KLSCAN_0065",
  // etc. — used to match against scansList.fileName, which is also a bare basename.
  private static string ExtractBasename(string path)
  {
    if (string.IsNullOrWhiteSpace(path)) return "";
    var trimmed = path.Trim();
    var lastSlash = trimmed.LastIndexOfAny(new[] { '/', '\\' });
    var name = lastSlash >= 0 ? trimmed.Substring(lastSlash + 1) : trimmed;
    name = StripImageExtension(name);
    foreach (var suffix in new[] { "_large", "_full", "_small" })
      if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        return name.Substring(0, name.Length - suffix.Length);
    return name;
  }

  private async Task<List<int>> LoadArtlistIdsForShow(int showId)
  {
    var ids = new List<int>();
    await using var conn = new NpgsqlConnection(_connectionString);
    await conn.OpenAsync();
    await using var cmd = new NpgsqlCommand("SELECT id_field FROM artlist WHERE show_id = @sid", conn);
    cmd.Parameters.AddWithValue("sid", showId);
    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync()) ids.Add(reader.GetInt32(0));
    return ids;
  }

  private async Task<List<ArtlistItemRow>> LoadArtlistItems(List<int> artlistIds)
  {
    var rows = new List<ArtlistItemRow>();
    await using var conn = new NpgsqlConnection(_connectionString);
    await conn.OpenAsync();
    await using var cmd = new NpgsqlCommand(
      @"SELECT id_field, artlist_id, sequence, artwork_id, artwork_image_id,
               photo_id, item_type, url, notes
        FROM artlist_item
        WHERE artlist_id = ANY(@ids)
        ORDER BY sequence ASC NULLS LAST, id_field ASC", conn);
    cmd.Parameters.AddWithValue("ids", artlistIds.ToArray());
    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
      rows.Add(new ArtlistItemRow
      {
        Id              = reader.GetInt32(0),
        ArtlistId       = reader.GetInt32(1),
        Sequence        = reader.IsDBNull(2) ? null : Convert.ToInt32(reader.GetValue(2)),
        ArtworkId       = reader.IsDBNull(3) ? null : Convert.ToInt32(reader.GetValue(3)),
        ArtworkImageId  = reader.IsDBNull(4) ? null : Convert.ToInt64(reader.GetValue(4)),
        PhotoId         = reader.IsDBNull(5) ? null : Convert.ToInt32(reader.GetValue(5)),
        ItemType        = reader.IsDBNull(6) ? "" : reader.GetString(6),
        Url             = reader.IsDBNull(7) ? "" : reader.GetString(7),
        Notes           = reader.IsDBNull(8) ? "" : reader.GetString(8),
      });
    }
    return rows;
  }

  private async Task<Dictionary<int, ArtworkLookup>> LoadArtworkLookups(List<ArtlistItemRow> items)
  {
    var ids = items.Where(i => i.ArtworkId.HasValue).Select(i => i.ArtworkId!.Value).Distinct().ToArray();
    var dict = new Dictionary<int, ArtworkLookup>();
    if (ids.Length == 0) return dict;

    await using var conn = new NpgsqlConnection(_connectionString);
    await conn.OpenAsync();
    await using var cmd = new NpgsqlCommand(
      @"SELECT id_field, filename, title, human_readable_id, create_dt, medium,
               dimensions, folded_dimensions, notes, unsigned
        FROM artwork WHERE id_field = ANY(@ids)", conn);
    cmd.Parameters.AddWithValue("ids", ids);
    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
      dict[reader.GetInt32(0)] = new ArtworkLookup
      {
        FileName         = reader.IsDBNull(1) ? "" : reader.GetString(1),
        Title            = reader.IsDBNull(2) ? "" : reader.GetString(2),
        HumanId          = reader.IsDBNull(3) ? "" : reader.GetString(3),
        CreateDt         = reader.IsDBNull(4) ? DateTime.MinValue : reader.GetDateTime(4),
        Medium           = reader.IsDBNull(5) ? "" : reader.GetString(5),
        Dimensions       = reader.IsDBNull(6) ? "" : reader.GetString(6),
        FoldedDimensions = reader.IsDBNull(7) ? "" : reader.GetString(7),
        Notes            = reader.IsDBNull(8) ? "" : reader.GetString(8),
        Unsigned         = !reader.IsDBNull(9) && reader.GetBoolean(9),
      };
    }
    return dict;
  }

  private async Task<Dictionary<int, PhotoLookup>> LoadPhotoLookups(List<ArtlistItemRow> items)
  {
    var ids = items.Where(i => i.PhotoId.HasValue).Select(i => i.PhotoId!.Value).Distinct().ToArray();
    var dict = new Dictionary<int, PhotoLookup>();
    if (ids.Length == 0) return dict;

    await using var conn = new NpgsqlConnection(_connectionString);
    await conn.OpenAsync();
    await using var cmd = new NpgsqlCommand(
      "SELECT id_field, file_location, description FROM photo WHERE id_field = ANY(@ids)", conn);
    cmd.Parameters.AddWithValue("ids", ids);
    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
      dict[reader.GetInt32(0)] = new PhotoLookup
      {
        FileLocation = reader.IsDBNull(1) ? "" : reader.GetString(1),
        Description  = reader.IsDBNull(2) ? "" : reader.GetString(2),
      };
    }
    return dict;
  }

  private async Task<Dictionary<long, ArtworkImageLookup>> LoadArtworkImageLookups(List<ArtlistItemRow> items)
  {
    var ids = items.Where(i => i.ArtworkImageId.HasValue).Select(i => i.ArtworkImageId!.Value).Distinct().ToArray();
    var dict = new Dictionary<long, ArtworkImageLookup>();
    if (ids.Length == 0) return dict;

    // artwork_image.id_field is INTEGER; cast the long[] params accordingly.
    var intIds = ids.Select(x => (int)x).ToArray();
    await using var conn = new NpgsqlConnection(_connectionString);
    await conn.OpenAsync();
    await using var cmd = new NpgsqlCommand(
      "SELECT id_field, url, view FROM artwork_image WHERE id_field = ANY(@ids)", conn);
    cmd.Parameters.AddWithValue("ids", intIds);
    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
      dict[reader.GetInt32(0)] = new ArtworkImageLookup
      {
        Url  = reader.IsDBNull(1) ? "" : reader.GetString(1),
        View = reader.IsDBNull(2) ? "" : reader.GetString(2),
      };
    }
    return dict;
  }

  private static string BuildShowTitle(ShowRow show)
  {
    if (!string.IsNullOrWhiteSpace(show.ShowName)) return show.ShowName;
    var range = FormatDateRange(show.StartDt, show.EndDt);
    // Fall back to the YEAR field when no start/end dates are recorded.
    if (string.IsNullOrEmpty(range) && show.Year.HasValue)
      range = show.Year.Value.ToString();
    var rangeSuffix = string.IsNullOrEmpty(range) ? "" : $" ({range})";
    return show.ShowType?.ToUpperInvariant() switch
    {
      "SOLO"    => $"Keith Long Solo Show{rangeSuffix}",
      "2PERSON" => $"Two-Person Show{rangeSuffix}",
      _         => $"Group Show{rangeSuffix}",
    };
  }

  private static string FormatShowType(string type) => type?.ToUpperInvariant() switch
  {
    "SOLO"    => "Solo",
    "2PERSON" => "Two-Person",
    "GROUP"   => "Group",
    _         => type ?? "",
  };

  private static string FormatDateRange(DateTime? a, DateTime? b)
  {
    static string MonthYear(DateTime d) => d.ToString("MMMM yyyy", CultureInfo.InvariantCulture);
    if (a.HasValue && b.HasValue) return $"{MonthYear(a.Value)} - {MonthYear(b.Value)}";
    if (a.HasValue) return MonthYear(a.Value);
    if (b.HasValue) return MonthYear(b.Value);
    return "";
  }
}
