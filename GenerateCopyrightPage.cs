using System.Text;

namespace ArtWorkHTML;

public partial class ArtworkHTML
{
  private async Task GenerateCopyrightPage()
  {
    var html = new StringBuilder();
    html.AppendLine(GetHtmlHeader("Copyright — Keith Long Archive"));
    html.AppendLine(@"
    <div class='landing-header'>
      <h1>Keith Long Archive</h1>
      <p class='subtitle'>Copyright &amp; Usage Details</p>
    </div>
    <div class='landing-header'>
        <p class='subtitle'><a href='index.html'>← Back to Archive</a></p>
        <div class='landing-content'>
            <p>Content coming soon.</p>
        </div>
    </div>");
    html.AppendLine(GetHtmlFooter());
    await File.WriteAllTextAsync(Path.Combine(_outputDirectory, "copyright.html"), html.ToString());
  }
}
