using Npgsql;
using System.Text;

namespace ArtWorkHTML;

public partial class ArtworkHTML
{
  private sealed class ArchiveImageRow
  {
    public int ImageId;
    public string? Url;
    public string View = "";
    public string Photographer = "";
  }

  private sealed class ArchiveRow
  {
    public string HumanId = "";
    public string Title = "";
    public string TypeName = "";
    public string SubtypeName = "";
    public string Extent = "";
    public string Scope = "";
    public string Notes = "";
    public string ArchiveContainer = "";
    public string ChildContainer = "";
    public DateTime? ItemCreateDt;
    public List<ArchiveImageRow> Images = [];
  }

  // Mirrors the artwork/artwork_image pattern (see GenerateArtworkPages): one
  // flat gallery of every archive record, each with all of its archive_image
  // rows shown (a main image plus thumb-buttons for any additional views).
  // Admin-only, like scans.html/polaroids.html — not linked from the public site.
  private const string archiveSQL = @"
    SELECT
      a.airtable_id, a.human_readable_id, a.title, a.extent, a.scope, a.notes,
      a.archive_container, a.child_container, a.item_create_dt,
      at.type AS type_name, ast.sub_type AS subtype_name,
      ai.id_field, ai.url, ai.view, ai.photographer
    FROM archive a
    LEFT JOIN archive_type at ON a.type ->> 0 = at.airtable_id
    LEFT JOIN archive_subtype ast ON a.subtype ->> 0 = ast.airtable_id
    LEFT JOIN archive_image ai ON ai.archive_id ->> 0 = a.airtable_id
    ORDER BY a.human_readable_id, ai.id_field";

  private async Task<List<ArchiveRow>> LoadArchiveRowsAsync()
  {
    var rows = new List<ArchiveRow>();
    var byAirtableId = new Dictionary<string, ArchiveRow>();

    await using var conn = new NpgsqlConnection(_connectionString);
    await conn.OpenAsync();
    await using var cmd = new NpgsqlCommand(archiveSQL, conn);
    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
      var airtableId = reader.GetString(0);
      if (!byAirtableId.TryGetValue(airtableId, out var row))
      {
        row = new ArchiveRow
        {
          HumanId          = reader.IsDBNull(1) ? "" : reader.GetString(1),
          Title            = reader.IsDBNull(2) ? "" : reader.GetString(2),
          Extent           = reader.IsDBNull(3) ? "" : reader.GetString(3),
          Scope            = reader.IsDBNull(4) ? "" : reader.GetString(4),
          Notes            = reader.IsDBNull(5) ? "" : reader.GetString(5),
          ArchiveContainer = reader.IsDBNull(6) ? "" : reader.GetString(6),
          ChildContainer   = reader.IsDBNull(7) ? "" : reader.GetString(7),
          ItemCreateDt     = reader.IsDBNull(8) ? null : reader.GetDateTime(8),
          TypeName         = reader.IsDBNull(9) ? "" : reader.GetString(9),
          SubtypeName      = reader.IsDBNull(10) ? "" : reader.GetString(10),
        };
        byAirtableId[airtableId] = row;
        rows.Add(row);
      }

      // LEFT JOIN means an archive record with zero images still produces one
      // row, with every ai.* column null — id_field null is how we tell "no
      // image" apart from "an image with no id" (which can't happen).
      if (!reader.IsDBNull(11))
      {
        row.Images.Add(new ArchiveImageRow
        {
          ImageId      = reader.GetInt32(11),
          Url          = reader.IsDBNull(12) ? null : reader.GetString(12),
          View         = reader.IsDBNull(13) ? "" : reader.GetString(13),
          Photographer = reader.IsDBNull(14) ? "" : reader.GetString(14),
        });
      }
    }
    return rows;
  }

  // Resolves one archive_image row to (previewUrl, fullUrl, embeddable).
  // embeddable=false means url is a link to follow, not an image to render
  // inline (e.g. a Flickr photo-page URL rather than a direct image file).
  private static (string? preview, string full, bool embeddable) ResolveArchiveImageUrl(ArchiveImageRow img)
  {
    // No URL -> an Airtable attachment, downloaded by AirtableImageDownloader
    // and uploaded to S3 under the same atch/<prefix>_<id>_<size>.jpg
    // convention artwork attachments use (see S3_ARTWORK_IMAGE_URL).
    if (string.IsNullOrEmpty(img.Url))
    {
      var preview = $"{ImageBaseUrl}atch/archive_{img.ImageId}_large.jpg";
      var full = $"{ImageBaseUrl}atch/archive_{img.ImageId}_full.jpg";
      return (preview, full, true);
    }

    if (img.Url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
        img.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
    {
      var ext = Path.GetExtension(img.Url);
      var isImageExt = ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                     || ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
                     || ext.Equals(".png", StringComparison.OrdinalIgnoreCase)
                     || ext.Equals(".gif", StringComparison.OrdinalIgnoreCase);
      return isImageExt ? (img.Url, img.Url, true) : (null, img.Url, false);
    }

    var (p, f) = BuildJpgUrls(img.Url);
    return (p, f, true);
  }

  private async Task GenerateArchivePages()
  {
    var rows = await LoadArchiveRowsAsync();
    await WriteArchivePage(rows);
    Console.WriteLine($"  ✓ archive.html ({rows.Count} archive items, {rows.Sum(r => r.Images.Count)} images)");
  }

  private async Task WriteArchivePage(List<ArchiveRow> rows)
  {
    var html = new StringBuilder();
    html.AppendLine(GetHtmlHeader("Archive - Keith Long Archive", canonicalPath: "archive.html", noindex: true));

    html.AppendLine(@"
    <div class='container'>
        <h1>Archive</h1>
        <p class='subtitle'><a id='back-link' href='admin.html'>← Back to Admin</a></p>
    </div>
    <div class='page-controls'>
        <span class='page-controls-label'>Hover effects:</span>
        <label><input type='checkbox' id='chk-thumb-hover' checked onchange='document.body.classList.toggle(""no-thumb-hover"", !this.checked)'> Thumbnail preview (p)</label>
        <label><input type='checkbox' id='chk-image-hover' checked onchange='document.body.classList.toggle(""no-image-hover"", !this.checked)'> Image zoom (z)</label>
    </div>");
    html.AppendLine(SharedScriptTag(TagsScriptFile));
    html.AppendLine(SharedScriptTag(KeysScriptFile));
    html.AppendLine(SharedScriptTag(ThumbsScriptFile));
    html.AppendLine(@"
    <div id='tag-title' class='tag-title-banner' style='display:none'></div>
    <div class='container'>");

    html.AppendLine("<div class='gallery' style='font-size: x-small;'>");

    foreach (var a in rows)
    {
      html.AppendLine($@"<div class='gallery-item'{DateAttr(a.ItemCreateDt ?? DateTime.MinValue)}>");

      if (a.Images.Count == 0)
      {
        html.AppendLine("  <div class='desc'>(no image)</div>");
      }
      else
      {
        // Main image: the first embeddable one. If none are embeddable (e.g. the
        // record's only image is an external non-image link), fall back to a
        // plain outbound link instead of a broken <img>.
        var main = a.Images.FirstOrDefault(i => ResolveArchiveImageUrl(i).embeddable) ?? a.Images[0];
        var (mainPreview, mainFull, mainEmbeddable) = ResolveArchiveImageUrl(main);
        if (mainEmbeddable)
        {
          html.AppendLine($@"  <a href='{mainFull}' rel='noopener noreferrer'><img src='{mainPreview}' title='(click for full size)' loading='lazy'/></a><br/>");
        }
        else
        {
          html.AppendLine($@"  <div class='desc'><a class='desc' href='{mainFull}' target='_blank' rel='noopener noreferrer'>[external link]</a></div>");
        }
        if (!string.IsNullOrEmpty(main.Photographer))
          html.AppendLine($"  <div class='desc'>{EscapeHtml(main.Photographer)}</div>");

        // Every other image on this record becomes a small thumb-button, the
        // same "additional views" convention artwork.html uses for Front/Back/
        // Paper/Polaroid — labeled with the image's own view text since
        // archive_image views aren't a fixed set.
        var rest = a.Images.Where(i => i != main).ToList();
        if (rest.Count > 0)
        {
          var thumbButtons = new List<string>();
          int n = 1;
          foreach (var img in rest)
          {
            var (preview, full, embeddable) = ResolveArchiveImageUrl(img);
            var label = string.IsNullOrEmpty(img.View) ? $"Image {n}" : img.View;
            if (embeddable)
              thumbButtons.Add($"<a href='{full}' target='_blank' rel='noopener noreferrer' class='thumb-button' title='{EscapeHtml(label)}'><img src='{preview}' width='40' height='40' data-large-src='{preview}' onload='applyThumbSize(this)' loading='lazy' /></a>");
            else
              thumbButtons.Add($"<a href='{full}' target='_blank' rel='noopener noreferrer' class='thumb-button' title='{EscapeHtml(label)} (external link)'>[{EscapeHtml(label)}]</a>");
            n++;
          }
          html.AppendLine("  <div class='thumb-buttons'>");
          html.AppendLine($"    {string.Join(" ", thumbButtons)}");
          html.AppendLine("  </div>");
        }
      }

      var typeLine = string.Join(" / ", new[] { a.TypeName, a.SubtypeName }.Where(s => !string.IsNullOrEmpty(s)));
      var containerLine = string.Join(", ", new[] { a.ArchiveContainer, a.ChildContainer }.Where(s => !string.IsNullOrEmpty(s)));

      html.AppendLine("  <div class='desc item-description'>");
      html.AppendLine($"    {BlankOrWithBR(a.Title, "  ")}");
      html.AppendLine($"    {BlankOrWithBR(a.HumanId, "  ")}");
      html.AppendLine($"    {BlankOrWithBR(typeLine, "  ")}");
      html.AppendLine($"    {BlankOrWithBR(DateOrEmpty(a.ItemCreateDt ?? DateTime.MinValue), "  ")}");
      html.AppendLine($"    {BlankOrWithBR(a.Extent, "  ")}");
      html.AppendLine($"    {BlankOrWithBR(containerLine, "  ")}");
      html.AppendLine($"    {BlankOrWithBR(a.Scope, "  ")}");
      html.AppendLine($"    {BlankOrWithBR(a.Notes, "  Notes: ")}");
      html.AppendLine("  </div>");

      html.AppendLine("</div>  <!-- gallery item -->");
    }

    html.AppendLine("</div>");
    html.AppendLine(GetLightboxHtml());
    html.AppendLine(GetLightboxScriptTag());
    html.AppendLine(GetHtmlFooter());

    await File.WriteAllTextAsync(Path.Combine(_outputDirectory, "archive.html"), html.ToString());
  }
}
