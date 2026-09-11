using System.Text;

namespace ArtWorkHTML;

public partial class ArtworkHTML
{
  // Groups a raw art.errors message into a stable "type" for counting/filtering.
  // Most messages are already static strings; the few that embed per-artwork
  // dynamic text (an id or a filename) are collapsed to one bucket each so the
  // errors.html summary shows "47x Duplicate humanId" rather than 47 separate rows.
  private static string ErrorBucketKey(string err)
  {
    if (err.StartsWith("Duplicate humanId")) return "Duplicate humanId";
    if (err.StartsWith("Filename '") && err.Contains("does not match expected sketchbook format"))
      return "Filename does not match expected sketchbook format";
    return err;
  }

  // Turns an error bucket key into a filesystem/URL-safe slug for its detail page
  // (e.g. "Missing back photo" -> "missing-back-photo").
  private static string SlugifyErrorKey(string key)
  {
    var chars = key.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray();
    var slug = new string(chars);
    while (slug.Contains("--"))
      slug = slug.Replace("--", "-");
    slug = slug.Trim('-');
    return string.IsNullOrEmpty(slug) ? "error" : slug;
  }

  // Writes errors.html (a summary table of error types + counts + a View button
  // per type) plus one errors-<slug>.html gallery page per error type, filtered
  // to just the artworks with that error. _errorCounts is already populated by
  // the artwork.html trackErrors pass that ran just before this is called.
  private async Task WriteErrorPages(List<Artwork> erroredArtworks)
  {
    var types = _errorCounts
      .OrderByDescending(kvp => kvp.Value)
      .Select(kvp => (Key: kvp.Key, Count: kvp.Value, Slug: SlugifyErrorKey(kvp.Key)))
      .ToList();

    var html = new StringBuilder();
    html.AppendLine(GetHtmlHeader("Errors - Keith Long Archive", canonicalPath: "errors.html", noindex: true));
    html.AppendLine(@"
    <style>
      .error-type-table { max-width: 700px; margin: 30px auto; border-collapse: collapse; width: 100%; }
      .error-type-table th, .error-type-table td { padding: 10px 14px; border-bottom: 1px solid #ddd; text-align: left; }
      .error-type-table th { color: #2c3e50; }
      .error-type-table td.count { text-align: right; font-variant-numeric: tabular-nums; }
      .error-type-table td.view { text-align: right; }
    </style>
    <div class='landing-header'>
      <h1>Artwork Errors</h1>
      <p class='subtitle'><a href='admin.html'>← Back to Admin</a></p>
    </div>
    <div class='container'>");

    if (types.Count == 0)
    {
      html.AppendLine("      <p style='text-align:center; color:#555;'>No errors on the last generation.</p>");
    }
    else
    {
      html.AppendLine("      <table class='error-type-table'>");
      html.AppendLine("        <tr><th>Error type</th><th>Count</th><th></th></tr>");
      foreach (var t in types)
      {
        html.AppendLine(
          $"        <tr><td>{EscapeHtml(t.Key)}</td><td class='count'>{t.Count}</td>" +
          $"<td class='view'><a class='nav-button nav-button-sm' href='errors-{t.Slug}.html?show=all&amp;back=errors.html&amp;backlabel=Errors'>View</a></td></tr>");
      }
      html.AppendLine("      </table>");
    }
    html.AppendLine("    </div>");
    html.AppendLine(GetHtmlFooter());
    await File.WriteAllTextAsync(Path.Combine(_outputDirectory, "errors.html"), html.ToString());

    foreach (var t in types)
    {
      var filtered = erroredArtworks.Where(a => a.errors.Any(e => ErrorBucketKey(e) == t.Key));
      await WriteArtworkGalleryPage(
        $"errors-{t.Slug}.html",
        $"Errors - {t.Key}",
        filtered,
        includeTypeFilter: true,
        trackErrors: false,
        noindex: true);
    }
  }
}
