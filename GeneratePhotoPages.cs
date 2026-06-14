using Npgsql;
using System.Text;

namespace ArtWorkHTML;

public partial class ArtworkHTML
{
  private sealed class PhotoCategory
  {
    public string AirtableId = "";
    public string Code = "";
    public string Name = "";
  }

  private sealed class PhotoRow
  {
    public string Url = "";
    public DateTime? Date;
    public int? Year;
    public string Notes = "";
    public string People = "";
    public string Location = "";
    public string CatName = "";
  }

  private const string UncategorizedPhotos = "Uncategorized";

  // Generates the Photos section: photo.html (a category index with counts) plus
  // one detail page per category under the photo/ subdirectory. Photos are pulled
  // straight from the photo table; the image URL is the file_location, or the
  // synthesized sscan/KL_<code>_<image_number> form when file_location is null.
  private async Task GeneratePhotoPages()
  {
    var categories = await LoadPhotoCategoriesAsync();
    var photos = await LoadPhotosAsync();

    // Group by category display name; null/empty category -> "Uncategorized".
    var groups = new Dictionary<string, List<PhotoRow>>(StringComparer.OrdinalIgnoreCase);
    foreach (var p in photos)
    {
      var key = string.IsNullOrWhiteSpace(p.CatName) ? UncategorizedPhotos : p.CatName;
      if (!groups.TryGetValue(key, out var list)) { list = new List<PhotoRow>(); groups[key] = list; }
      list.Add(p);
    }

    var photoDir = Path.Combine(_outputDirectory, "photo");
    Directory.CreateDirectory(photoDir);

    foreach (var kv in groups)
      await WritePhotoCategoryPage(kv.Key, kv.Value, photoDir);

    await WritePhotoIndex(categories, groups);

    Console.WriteLine($"  ✓ photo.html ({photos.Count} photos in {groups.Count} categories)");
  }

  private async Task<List<PhotoCategory>> LoadPhotoCategoriesAsync()
  {
    const string sql = "SELECT airtable_id, code, catagory FROM photo_catagory ORDER BY catagory NULLS LAST";
    var cats = new List<PhotoCategory>();
    await using var conn = new NpgsqlConnection(_connectionString);
    await conn.OpenAsync();
    await using var cmd = new NpgsqlCommand(sql, conn);
    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
      cats.Add(new PhotoCategory
      {
        AirtableId = reader.IsDBNull(0) ? "" : reader.GetString(0),
        Code       = reader.IsDBNull(1) ? "" : reader.GetString(1),
        Name       = reader.IsDBNull(2) ? "" : reader.GetString(2),
      });
    }
    return cats;
  }

  private async Task<List<PhotoRow>> LoadPhotosAsync()
  {
    const string sql = @"
      SELECT
        COALESCE(p.file_location, CONCAT('sscan/KL_', pc.code, '_', p.image_number)) AS url,
        p.date, p.year, p.notes, p.people, p.location,
        pc.catagory AS cat_name
      FROM photo p
      LEFT JOIN photo_catagory pc ON p.catagory ->> 0 = pc.airtable_id
      ORDER BY pc.catagory NULLS LAST, p.year NULLS LAST, p.image_number NULLS LAST";

    var photos = new List<PhotoRow>();
    await using var conn = new NpgsqlConnection(_connectionString);
    await conn.OpenAsync();
    await using var cmd = new NpgsqlCommand(sql, conn);
    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
      photos.Add(new PhotoRow
      {
        Url      = reader.IsDBNull(0) ? "" : reader.GetString(0),
        Date     = reader.IsDBNull(1) ? null : reader.GetDateTime(1),
        Year     = reader.IsDBNull(2) ? null : Convert.ToInt32(reader.GetValue(2)),
        Notes    = reader.IsDBNull(3) ? "" : reader.GetString(3),
        People   = reader.IsDBNull(4) ? "" : reader.GetString(4),
        Location = reader.IsDBNull(5) ? "" : reader.GetString(5),
        CatName  = reader.IsDBNull(6) ? "" : reader.GetString(6),
      });
    }
    return photos;
  }

  private async Task WritePhotoCategoryPage(string catName, List<PhotoRow> photos, string photoDir)
  {
    const string pathPrefix = "../";
    var slug = MakeTag(catName);

    var html = new StringBuilder();
    html.AppendLine(GetHtmlHeader($"{catName} Photos - Keith Long Archive", pathPrefix));
    html.AppendLine($@"
    <div class='container'>
        <h1>{EscapeHtml(catName)} Photos</h1>
        <p class='subtitle'><a id='back-link' href='{pathPrefix}photo.html'>← Back to Photos</a> &middot; <a href='{pathPrefix}index.html'>Home</a></p>");
    html.AppendLine("        <div class='gallery'>");
    foreach (var p in photos)
      html.AppendLine(RenderPhoto(p));
    html.AppendLine("        </div>");
    html.AppendLine("    </div>");
    html.AppendLine($"<script>{GetTagsScript()}</script>");
    html.AppendLine(GetLightboxHtml());
    html.AppendLine(GetLightboxScriptTag(pathPrefix));
    html.AppendLine(GetHtmlFooter(pathPrefix));

    await File.WriteAllTextAsync(Path.Combine(photoDir, $"{slug}.html"), html.ToString());
  }

  private string RenderPhoto(PhotoRow p)
  {
    if (string.IsNullOrWhiteSpace(p.Url)) return "";
    var (preview, full) = BuildJpgUrls(p.Url);

    var sb = new StringBuilder();
    sb.AppendLine("<div class='gallery-item tag-active'>");
    sb.AppendLine($"  <a href='{full}' rel='noopener noreferrer'><img src='{preview}' loading='lazy' title='(click for full size)'/></a>");
    sb.AppendLine("  <div class='desc item-description'>");
    sb.AppendLine($"    {BlankOrWithBR(DateOrEmpty(p.Date ?? DateTime.MinValue), "  ")}");
    if (p.Year.HasValue && p.Year.Value > 1900)
      sb.AppendLine($"    {BlankOrWithBR(p.Year.Value.ToString(), "  ")}");
    sb.AppendLine($"    {BlankOrWithBR(EscapeHtml(p.People), "  ")}");
    sb.AppendLine($"    {BlankOrWithBR(EscapeHtml(p.Location), "  ")}");
    sb.AppendLine($"    {BlankOrWithBR(EscapeHtml(p.Notes).Replace("\n", "<br/>"), "  Notes: ")}");
    sb.AppendLine("  </div>");
    sb.AppendLine("</div>");
    return sb.ToString();
  }

  private async Task WritePhotoIndex(List<PhotoCategory> categories, Dictionary<string, List<PhotoRow>> groups)
  {
    var html = new StringBuilder();
    html.AppendLine(GetHtmlHeader("Photos - Keith Long Archive"));
    html.AppendLine(@"
    <div class='container landing-page'>
      <div class='landing-header'>
        <h1>Photos</h1>
        <p class='subtitle'><a id='back-link' href='index.html'>← Back to Home</a></p>
      </div>
      <div class='landing-content'>
        <p>Photographs from the Keith Long archive, organized by category.</p>
      </div>
      <div class='navigation'>");

    void Button(string name, int count)
    {
      var label = $"{EscapeHtml(name)} ({count})";
      if (count > 0)
        html.AppendLine($"        <div class='nav-button-wrap'><a href='photo/{MakeTag(name)}.html' class='nav-button'>{label}</a><div class='coming-soon'>&nbsp;</div></div>");
      else
        html.AppendLine($"        <div class='nav-button-wrap'><div class='nav-button nav-button-soon'>{label}</div><div class='coming-soon'>&nbsp;</div></div>");
    }

    foreach (var cat in categories)
      Button(cat.Name, groups.TryGetValue(cat.Name, out var list) ? list.Count : 0);

    if (groups.TryGetValue(UncategorizedPhotos, out var unc) && unc.Count > 0)
      Button(UncategorizedPhotos, unc.Count);

    html.AppendLine(@"
      </div>
    </div>");
    html.AppendLine($"<script>{GetTagsScript()}</script>");
    html.AppendLine(GetHtmlFooter());

    await File.WriteAllTextAsync(Path.Combine(_outputDirectory, "photo.html"), html.ToString());
  }
}
