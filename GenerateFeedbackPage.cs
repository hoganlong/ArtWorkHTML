using System.Text;

namespace ArtWorkHTML;

public partial class ArtworkHTML
{
  private async Task GenerateFeedbackPage()
  {
    var html = new StringBuilder();
    html.AppendLine(GetHtmlHeader("Feedback — Keith Long Archive"));
    html.AppendLine(@"
    <div class='landing-header'>
      <h1>Keith Long Archive</h1>
      <p class='subtitle'>Feedback</p>
    </div>
    <div class='landing-header'>
        <p class='subtitle'><a href='index.html'>← Back to Archive</a></p>
        <div class='landing-content'>
            <p>Content coming soon.</p>
        </div>
    </div>");
    html.AppendLine(GetHtmlFooter());
    await File.WriteAllTextAsync(Path.Combine(_outputDirectory, "feedback.html"), html.ToString());
  }
}
