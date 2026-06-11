using System.Text;

namespace ArtWorkHTML;

public partial class ArtworkHTML
{
  private async Task GenerateCreditsPage()
  {
    var html = new StringBuilder();
    html.AppendLine(GetHtmlHeader("Credits & Thanks — Keith Long Archive"));
    html.AppendLine(@"
    <div class='landing-header'>
      <h1>Keith Long Archive</h1>
      <p class='subtitle'>Credits &amp; Thanks</p>
    </div>
    <div class='landing-header'>
        <p class='subtitle'><a href='index.html'>← Back to Archive</a></p>
        <div class='landing-content'>
            <p>The Keith Long Archive is the result of a promise to my father to preserve his life’s work.  Having spent a career in IT I had the skills to make a website to present it to a larger audience which my father would have appreciated.  However, I didn’t have the skills to build an artist archive.  I’ve had to rely on a number of other people to help with that goal, and it is their work which has allowed this website to be created.  To them I give heartfelt thanks, I got lucky and found an amazing group of people to help, I could not have done this without them.  Details are below and they all have my deepest thanks and recommendation. Without their help I would not have been able to make my father’s lifetime of artwork available for your enjoyment on this website.</p>
            <!-- Per-person blurbs go here -->

            <div class='credit-person'>
              <div class='credit-name'>Mary Sabbatino</div>
              <div class='credit-role'>Galerie Lelong &amp; Co., New York</div>
              <p class='credit-blurb'>This archive began with a conversation with Mary Sabbatino. As Vice President and Partner of Galerie Lelong &amp; Co. — one of New York's most distinguished galleries, known for championing artists from across the world with integrity and vision.  A former student of Keith's, Mary has spoken warmly of the lasting impact he had on her, and that connection clearly never faded. It was her idea to create the archive, and Mary helped give it direction — offering perspective on how to approach the project thoughtfully and at the right scale. The team she connected us with has been outstanding in every way, reflecting her extraordinary network and her instinct for surrounding important work with the right people.</p>
              <p class='credit-links'>Website: <a href='https://galerielelong.com/' target='_blank' rel='noopener noreferrer'>galerielelong.com</a></p>
            </div>

            <div class='credit-person'>
              <div class='credit-name'>Anna Berlin</div>
              <div class='credit-role'>Lead Archivist</div>
              <p class='credit-blurb'>Every archive needs someone who can bring order to complexity without losing sight of what makes the work meaningful — and in Anna Berlin, we found exactly that person. With extensive experience on art archive projects and a background as an art educator, Anna brought both professional rigor and a deep sensitivity to the material. Working with her has been a genuine pleasure. She carries a calm, focused energy that settles the room, and her ability to organize large bodies of work with clarity and care has been central to everything this project has become.</p>
              <p class='credit-links'>Website: <a href='https://www.anna-berlin.com/' target='_blank' rel='noopener noreferrer'>anna-berlin.com</a></p>
            </div>

        </div>
    </div>");
    html.AppendLine(GetHtmlFooter());
    await File.WriteAllTextAsync(Path.Combine(_outputDirectory, "credits.html"), html.ToString());
  }
}
