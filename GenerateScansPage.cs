using System.Text;

namespace ArtWorkHTML;

public partial class ArtworkHTML
{
  private async Task GenerateScansPage(ArtList scansList)
  {
    var html = new StringBuilder();
    html.AppendLine(GetHtmlHeader("Scans - Keith Long Archive"));

    html.AppendLine(@"
    <div class='container'>
        <h1>Scans</h1>
        <p class='subtitle'><a id='back-link' href='index.html'>← Back to Home</a></p>
    </div>
    <div class='page-controls'>
        <span class='page-controls-label'>Hover effects:</span>
        <label><input type='checkbox' id='chk-image-hover' checked onchange='document.body.classList.toggle(""no-image-hover"", !this.checked)'> Image zoom (z)</label>
    </div>");
    html.AppendLine($"<script>{GetTagsScript()}</script>");
    html.AppendLine(@"<script>
    document.addEventListener('keydown', function(e) {
        if (e.target.tagName === 'INPUT' || e.target.tagName === 'TEXTAREA') return;
        if (e.key === 'z' || e.key === 'Z') document.getElementById('chk-image-hover')?.click();
        if (e.key === 't' || e.key === 'T') window.scrollTo({ top: 0, behavior: 'smooth' });
    });
    </script>
    <div id='tag-title' class='tag-title-banner' style='display:none'></div>
    <div class='container'>");

    html.AppendLine("<div class='gallery' style='font-size: x-small;'>");

    foreach (var artItem in scansList.artworks.OrderBy(x => x.Value.fileName))
    {
      Artwork art = artItem.Value;

      html.AppendLine($@"<div class='gallery-item tag-active'>");

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
