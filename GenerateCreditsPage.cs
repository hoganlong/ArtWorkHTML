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

        <!-- Per-person blurbs go below here -->

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

        <div class='credit-person'>
          <div class='credit-name'>Mike Marrella</div>
          <div class='credit-role'>Lead Art Handler</div>
          <p class='credit-blurb'>Mike Marrella is the kind of person every project hopes to find. As lead art handler, Mike took on not only the careful movement and management of the works but also the painstaking task of repairing pieces that had been damaged in storage — work that required patience, skill, and a real love for the art. No obstacle seemed to slow him down. Where others might have hesitated, Mike simply found a solution and kept moving, always with a friendly and positive spirit that made even the hardest days feel manageable.</p>
          <p class='credit-links'></p>
        </div>

        <div class='credit-person'>
          <div class='credit-name'>Tom Koehler</div>
          <div class='credit-role'>Art Handler</div>
          <p class='credit-blurb'>Working alongside Mike, Tom Koehler brought a specialist's eye to some of the archive's most delicate challenges. Tom's particular gift lies in color — matching paints and materials with a precision that has been invaluable in the restoration of damaged works. He has also played a key role in the careful packing of the collection, ensuring that each piece is protected and preserved for the long term. His steady, skilled presence has been an essential part of what makes this team work so well together.</p>
          <p class='credit-links'></p>
        </div>

        <div class='credit-person'>
          <div class='credit-name'>Rebel-Spirit Morgan</div>
          <div class='credit-role'>Assistant Archivist</div>
          <p class='credit-blurb'>Though Rebel-Spirit Morgan came to this project fresh out of school — with his sights set on a future in fashion — he has proven himself to be one of the most naturally gifted members of the team. His eye for detail is remarkable, his organizational instincts sharp, and his enthusiasm for learning new skills seemingly boundless. Time and again, Rebel has gone beyond what was asked, delivering work with a care and finish that speaks to the kind of professional he is already becoming. We are lucky to have had him.</p>
          <p class='credit-links'></p>
        </div>

        <div class='credit-person'>
          <div class='credit-name'>Tom Mueller</div>
          <div class='credit-role'>Photography</div>
          <p class='credit-blurb'>The works in this archive have been documented with the keen eye of Tom Mueller, a portrait and fine art photographer with more than 35 years of experience, based between Indianapolis and New York City. Tom's photographs bring the art to life on the page and screen in a way that does full justice to Keith Long's vision, and his collaborative, easygoing approach made the process a pleasure from start to finish.</p>
          <p class='credit-links'></p>
        </div>

        <div class='credit-person'>
          <div class='credit-name'></div>
          <div class='credit-role'></div>
          <p class='credit-blurb'></p>
          <p class='credit-links'></p>
        </div>






      </div>
    </div>");
    html.AppendLine(GetHtmlFooter());
    await File.WriteAllTextAsync(Path.Combine(_outputDirectory, "credits.html"), html.ToString());
  }
}
