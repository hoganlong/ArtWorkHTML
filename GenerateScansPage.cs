using System.Text;

namespace ArtWorkHTML;

public partial class ArtworkHTML
{
  private async Task GenerateScansPage(ArtList scansList)
  {
    var html = new StringBuilder();
    html.AppendLine(GetHtmlHeader("Scans - Keith Long Archive", canonicalPath: "scans.html", noindex: true));

    html.AppendLine(@"
    <div class='container'>
        <h1>Scans</h1>
        <p class='subtitle'><a id='back-link' href='admin.html'>← Back to Admin</a></p>
    </div>
    <div class='page-controls'>
        <span class='page-controls-label'>Hover effects:</span>
        <label><input type='checkbox' id='chk-image-hover' checked onchange='document.body.classList.toggle(""no-image-hover"", !this.checked)'> Image zoom (z)</label>
    </div>");
    html.AppendLine(SharedScriptTag(TagsScriptFile));
    html.AppendLine(SharedScriptTag(KeysScriptFile));
    html.AppendLine(@"
    <div id='tag-title' class='tag-title-banner' style='display:none'></div>
    <div class='container'>");

    html.AppendLine("<div class='gallery' style='font-size: x-small;'>");

    foreach (var artItem in scansList.artworks.OrderBy(x => x.Value.fileName))
    {
      Artwork art = artItem.Value;

      // No baked-in tag-active here: visibility is left to tags.js so the date filter
      // works, e.g. scans.html?show=d:8/27/2026 for everything uploaded that day. A bare
      // URL still shows every item, since no filter at all means show all. The date is
      // the S3 object's LastModified (the same one displayed below), so on this page the
      // filter reads as "when was this file uploaded".
      html.AppendLine($@"<div class='gallery-item'{DateAttr(art.ctDate)}>");

      if (art.states.HasFlag(StatesType.jpgFound))
      {
        html.AppendLine($@"  <a href='{art.jpgFullURL}' rel='noopener noreferrer'><img src='{art.jpgURL}' title='(click for full size)' loading='lazy'/></a><br/>");
      }
      else
      {
        html.AppendLine($@"  <div class='desc'>(no JPG)</div>");
      }

      if (art.states.HasFlag(StatesType.tifFound))
        html.AppendLine($"  <div class='desc'><a class='desc' href='{art.tifURL}'>[tif file]</a></div>");

      html.AppendLine($"  <div class='desc item-description'>");
      html.AppendLine($"    {BlankOrWithBR(art.fileName, "  ")}");
      html.AppendLine($"    {BlankOrWithBR(DateOrEmpty(art.ctDate), "  ")}");
      html.AppendLine($"  </div>");

      html.AppendLine($"</div>  <!-- gallery item -->");
    }

    html.AppendLine(@"</div>");
    html.AppendLine(GetLightboxHtml());
    html.AppendLine(GetLightboxScriptTag());
    html.AppendLine(GetHtmlFooter());

    await File.WriteAllTextAsync(Path.Combine(_outputDirectory, "scans.html"), html.ToString());
  }
}
