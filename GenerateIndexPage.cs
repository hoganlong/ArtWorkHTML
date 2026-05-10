using Microsoft.Extensions.Configuration;
using System.Text;

namespace ArtWorkHTML;

public partial class ArtworkHTML
{
  private async Task GenerateIndexPage()
  {
    var html = new StringBuilder();
    html.AppendLine(GetHtmlHeader("Keith Long Archive"));
    html.AppendLine(@"
    <div class='container landing-page'>
        <div class='landing-header'>
            <h1>Keith Long Archive</h1>
            <p class='subtitle'>A Digital Archive of a Lifetime of Artwork</p>
        </div>
        <div class='landing-content'>
            <p>Welcome to the Keith Long Archive, an attempt to create visual documentation of an artist's lifetime of work.</p>
            <p>It was created by his son Hogan Long to honor his father's work and to share it with a wider audience as his father wished.</p>
            <p>It includes photographs of paintings, drawings, sculptures and wall constructions,
            as well as scans of his sketchbooks, drawings, show announcements, reviews, and public media.</p>
            <p>If you know of any pieces that are not included in the archive, please contact us at keithlongarchive AT gmail DOT com.</p>
            <p>If you have a piece we have not included, please include a photo of the piece and any information you have about it,
            such as title, date, medium, dimensions, purchase date, and other relevant details.</p>
            <p>We also welcome comments or questions about the archive.</p>
        </div>
        <div class='navigation'>
            <div class='nav-button-wrap'><a href='artwork.html?show=all' class='nav-button'>Browse All Artworks</a><div class='coming-soon'>&nbsp;</div></div>
            <div class='nav-button-wrap'><a href='polaroids.html?show=all' class='nav-button'>Polaroids</a><div class='coming-soon'>&nbsp;</div></div>
            <div class='nav-button-wrap'><a href='scans.html' class='nav-button'>Scans</a><div class='coming-soon'>&nbsp;</div></div>
            <div class='nav-button-wrap'><a href='sketchbooks.html' class='nav-button'>Sketchbooks</a><div class='coming-soon'>&nbsp;</div></div>
            <div class='nav-button-wrap'><div class='nav-button nav-button-soon'>Shows</div><div class='coming-soon'>coming soon</div></div>
            <div class='nav-button-wrap'><div class='nav-button nav-button-soon'>Media</div><div class='coming-soon'>coming soon</div></div>
            <div class='break-point'></div>
            <div class='nav-button-wrap'><a href='statistics.html' class='nav-button'>Archive Statistics</a><div class='coming-soon'>&nbsp;</div></div>
        </div>
    </div>");
    html.AppendLine(GetHtmlFooter());

    await File.WriteAllTextAsync(Path.Combine(_outputDirectory, "index.html"), html.ToString());
  }
  
}