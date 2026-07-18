using Microsoft.Extensions.Configuration;
using System.Text;

namespace ArtWorkHTML;

public partial class ArtworkHTML
{
  // Default destination of the index "browse" split button. To change which
  // type page is the main click action, set this to another FileName from
  // the ArtworkTypePages list in ArtworkHTML.cs.
  private const string DefaultBrowsePageFileName = "artwork-wall-sculpture.html";

  private async Task GenerateIndexPage()
  {
    var defaultPage = ArtworkTypePages.First(p => p.FileName == DefaultBrowsePageFileName);

    var menuItems = new StringBuilder();
    menuItems.AppendLine("                    <li role='none'><a role='menuitem' href='artwork.html?show=all'>Browse All Artworks</a></li>");
    foreach (var p in ArtworkTypePages)
      menuItems.AppendLine($"                    <li role='none'><a role='menuitem' href='{p.FileName}?show=all'>{EscapeHtml(p.Title)}</a></li>");

    var browseButton = $@"<div class='split-button'>
                <a href='{defaultPage.FileName}?show=all' class='nav-button split-button-main'>{EscapeHtml(defaultPage.Title)}</a>
                <button type='button' class='nav-button split-button-toggle' aria-haspopup='true' aria-expanded='false' aria-label='Choose artwork category'>&#x25BE;</button>
                <ul class='split-button-menu' role='menu'>
{menuItems}                </ul>
              </div>";

    var html = new StringBuilder();
    html.AppendLine(GetHtmlHeader("Keith Long Archive"));
    html.AppendLine($@"
    <a href='admin.html' class='admin-only-link'>Admin only</a>
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
            <p>If you know of any pieces that are not included in the archive, please contact us at @@EMAIL@@.</p>
            <p>If you have a piece we have not included, please include a photo of the piece and any information you have about it,
            such as title, date, medium, dimensions, purchase date, and other relevant details.</p>
            <p>We also welcome comments or questions about the archive.</p>
        </div>
        <div class='navigation'>
            <div class='nav-button-wrap'>{browseButton}<div class='coming-soon'>&nbsp;</div></div>
            <div class='nav-button-wrap'><a href='sketchbooks.html' class='nav-button'>Sketchbooks</a><div class='coming-soon'>&nbsp;</div></div>
            <div class='nav-button-wrap'><a href='shows.html' class='nav-button'>Shows</a><div class='coming-soon'>&nbsp;</div></div>
            <div class='nav-button-wrap'><a href='photo.html' class='nav-button'>Photos</a><div class='coming-soon'>&nbsp;</div></div>
            <div class='nav-button-wrap'><div class='nav-button nav-button-soon'>Media</div><div class='coming-soon'>coming soon</div></div>
            <div class='break-point'></div>
            <div class='nav-button-wrap'><a href='statistics.html' class='nav-button'>Archive Statistics</a><div class='coming-soon'>&nbsp;</div></div>
        </div>
    </div>");
    html.AppendLine(@"
    <script>
    document.addEventListener('DOMContentLoaded', function() {
        var toggle = document.querySelector('.split-button-toggle');
        var menu = document.querySelector('.split-button-menu');
        var mainBtn = document.querySelector('.split-button-main');
        if (!toggle || !menu || !mainBtn) return;

        var STORAGE_KEY = 'kla_browse_default';

        // Restore previous selection if it still matches one of the menu items.
        try {
            var saved = JSON.parse(localStorage.getItem(STORAGE_KEY) || 'null');
            if (saved && saved.href && saved.label) {
                var match = false;
                menu.querySelectorAll('a[role=""menuitem""]').forEach(function(a) {
                    if (a.getAttribute('href') === saved.href) match = true;
                });
                if (match) {
                    mainBtn.setAttribute('href', saved.href);
                    mainBtn.textContent = saved.label;
                }
            }
        } catch (err) { /* ignore */ }

        function closeMenu() {
            menu.classList.remove('open');
            toggle.setAttribute('aria-expanded', 'false');
        }

        toggle.addEventListener('click', function(e) {
            e.preventDefault();
            var isOpen = menu.classList.toggle('open');
            toggle.setAttribute('aria-expanded', isOpen ? 'true' : 'false');
        });

        menu.querySelectorAll('a[role=""menuitem""]').forEach(function(a) {
            a.addEventListener('click', function(e) {
                e.preventDefault();
                var href = this.getAttribute('href');
                var label = this.textContent;
                mainBtn.setAttribute('href', href);
                mainBtn.textContent = label;
                try {
                    localStorage.setItem(STORAGE_KEY, JSON.stringify({ href: href, label: label }));
                } catch (err) { /* ignore */ }
                closeMenu();
            });
        });

        document.addEventListener('click', function(e) {
            if (!toggle.contains(e.target) && !menu.contains(e.target)) closeMenu();
        });

        document.addEventListener('keydown', function(e) {
            if (e.key === 'Escape') closeMenu();
        });
    });
    </script>");
    html.Replace("@@EMAIL@@", ObfuscatedEmailLink("keithlongarchive@gmail.com"));
    html.AppendLine(GetHtmlFooter());

    await File.WriteAllTextAsync(Path.Combine(_outputDirectory, "index.html"), html.ToString());
  }

}
