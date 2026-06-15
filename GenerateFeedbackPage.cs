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
            <p>If you want to contact us please use the email @@EMAIL@@. <BR>
            You can also contact Hogan Long on various social media via DM: <BR>
            <img src='https://archive.keithlong.com/icon/X.png'/><a href='https://x.com/HoganLong'>@hoganlong</a><BR>
            <img src='https://archive.keithlong.com/icon/instagram.png'/><a href='https://www.instagram.com/hogan.long/'>@hogan.long</a><BR>
            <img src='https://archive.keithlong.com/icon/facebook.png'/><a href='https://www.facebook.com/HoganLong/'>@hoganlong</a>
            </p>
        </div>
    </div>");
    html.Replace("@@EMAIL@@", ObfuscatedEmailLink("keithlongarchive@gmail.com"));
    html.AppendLine(GetHtmlFooter());
    await File.WriteAllTextAsync(Path.Combine(_outputDirectory, "feedback.html"), html.ToString());
  }
}
