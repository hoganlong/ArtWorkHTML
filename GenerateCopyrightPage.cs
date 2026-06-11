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
            <p>All the artwork on this site was created by my father and is copyright under his name.
             He passed away on September 27, 2025.  That means it will stay under copyright of the estate until 2095.
             The copyright is now held by his estate, managed by his son Hogan Long — for copyright questions contact keithlongarchive AT gmail DOT com.</p>
            <p>Copyright requires the person holding the copyright to enforce it, so I will explain how I will enforce it here.</p>
            <p><b>TLDR;</b> Put a link to this website if you use pictures from here.  If unsure about use, contact us and get our ok to use this content.</p>
            <p>First understand my philosophy: if the work is being shared, enjoyed, or used for personal or creative purposes, I am in favor of it.
            However, if the work is being used to make money by a company to gain profit, I am not.
             But I will allow licensing agreements and in most cases those will not be expensive. (Often as simple as including a reference to this website.)
             Some examples: a person loves a sketch and wants to print it out, frame it, and hang it on the wall to enjoy looking at every day.  This is fine.
             A person has a t-shirt company and wants to use reproductions of my dad's art on clothes they will sell.  That is not ok.  But if they contact me and
             I approve a design that includes the website url, then we have reached an agreement that costs them no money (we could also come to a cash agreement.)
             Ok, those are the general examples — now I will be more specific.  General rule: if you are not sure, email keithlongarchive at gmail to get an answer.</p>
            <h2>Things that are ok</h2>
             <ul>
             <li><b>Reproduction in article or media</b> - This is fine.  I'd like a link to the website included if possible.</li>
             <li><b>Personal Use</b> - That's fine, use it how you want.  This includes inspiration for other artwork.  If it is something that matters feel free to
             promote this website to others.  We like that.  It is nice if you do.</li>
             <li><b>Educational use</b> - Classroom presentations, research, and student projects are all welcome.</li>
             <li><b>Rework/remix into other art</b> - Sure.  That is how art is — art has always built on what came before — and I understand.  But there are certainly changes that don't count as rework.
              Downloading a tif and just changing the background color for example is not a rework.  You know when you are making something that is truly new.  Do that.</li>
            </ul>
            <p/>
            <h2>Things that are not ok</h2>
            <ul>
            <li><b>Selling reproductions</b> without getting permission</li>
            <li><b>Selling merch</b> with reproductions on it</li>
            </ul>
            <p/>
            <h2>A note on AI</h2>
            <p>The rule for AI is simple: <b>no use without credit.</b>
            This applies both to training data ingestion and to displaying or reproducing the images in AI-generated output.
            If your AI system provides attribution — a link or reference back to this website when the content is used — that is fine.
            If it does not, it is not ok.
            An example of acceptable attribution: the AI summary in Google Search includes links to its sources.
            That is the standard we expect.</p>
            <p><b>TLDR;</b> Put a link to this website if you use pictures from here.  If unsure about use, contact us and get our ok to use this content.</p>
        </div>
    </div>");
    html.AppendLine(GetHtmlFooter());
    await File.WriteAllTextAsync(Path.Combine(_outputDirectory, "copyright.html"), html.ToString());
  }
}
