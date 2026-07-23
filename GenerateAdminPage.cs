using System.Text;

namespace ArtWorkHTML;

public partial class ArtworkHTML
{
  // SHA-256 of the admin password string "useadmin", lower-case hex.
  // NOTE: this is a client-side-only soft gate, NOT real security. The hash and
  // the validation JS are both visible in page source; a plain SHA-256 of a short
  // word is trivially reversible. Do not put anything sensitive behind it. Real
  // auth must live server-side (see web-service-plan.md).
  private const string AdminPasswordHash =
    "aa0a9ba685e9f8cc694191e28e1bfd59bfb407e127a79b25f684b6a36607cce4";

  // localStorage key that stores the unlocked token (the password hash) so the
  // user does not have to re-enter the password on refresh. (Cookies are not used
  // because browsers don't persist them on file:// pages.)
  private const string AdminCookieName = "kla_admin";

  private async Task GenerateAdminPage(IReadOnlyList<string>? errorSummary = null)
  {
    var html = new StringBuilder();
    html.AppendLine(GetHtmlHeader("Admin - Keith Long Archive"));

    // Gate + (hidden) admin content. Same landing-page visual as index.html;
    // buttons here are placeholders to be filled in later.
    html.AppendLine(@"
    <style>
      #admin-gate { max-width: 360px; margin: 80px auto; padding: 30px;
        border: 1px solid #ccc; border-radius: 8px; text-align: center; background: #fff; }
      #admin-gate h1 { margin-top: 0; }
      #admin-gate input[type=password] { width: 100%; padding: 10px; font-size: 16px;
        box-sizing: border-box; margin: 12px 0; }
      #admin-gate button { padding: 10px 20px; font-size: 16px; cursor: pointer; }
      #admin-gate-error { color: #b00; min-height: 1.2em; margin-top: 10px; }
      .admin-bar { text-align: right; max-width: 1000px; margin: 0 auto 10px; }
      .admin-intro { max-width: 600px; margin: 40px auto 0; padding: 0 20px;
        text-align: center; color: #555; }
      .admin-section { max-width: 1000px; margin: 30px auto 0; padding: 0 20px; }
      .admin-section h2 { color: #2c3e50; font-size: 1.2em; margin-bottom: 10px; }
      .admin-errors { background: #f4f4f4; border: 1px solid #ddd; border-radius: 4px;
        padding: 14px 18px; font-family: 'Consolas','Courier New',monospace;
        font-size: 0.9em; line-height: 1.5; overflow-x: auto; color: #333; white-space: pre; }
    </style>

    <p class='admin-intro'>This page is for items that are used to develop the site.
    Current errors that need to be fixed and items that have not been given dates or
    categories. If you are visitor to the site, none of this will be interesting to
    you, don't have FOMO</p>

    <div id='admin-gate'>
        <h1>Admin</h1>
        <p>Enter the admin password to continue.</p>
        <form id='admin-gate-form'>
            <input type='password' id='admin-pass' autocomplete='current-password' autofocus placeholder='Password' />
            <button type='submit'>Unlock</button>
            <div id='admin-gate-error' role='alert'></div>
        </form>
    </div>

    <div id='admin-content' class='container landing-page' style='display:none'>
        <div class='admin-bar'>
            <button type='button' id='admin-logout' class='nav-button nav-button-sm'>Lock</button>
        </div>
        <div class='landing-header'>
            <h1>Admin</h1>
            <p class='subtitle'>Keith Long Archive &mdash; Administration</p>
        </div>
        <div class='navigation'>
            <div class='nav-button-wrap'><a href='index.html' class='nav-button'>Back to Site</a><div class='coming-soon'>&nbsp;</div></div>
            <div class='nav-button-wrap'><a href='errors.html?show=all&amp;back=admin.html&amp;backlabel=Admin' class='nav-button'>Errors</a><div class='coming-soon'>&nbsp;</div></div>
            <div class='nav-button-wrap'><a href='polaroids.html?show=all' class='nav-button'>Polaroids</a><div class='coming-soon'>&nbsp;</div></div>
            <div class='nav-button-wrap'><a href='scans.html' class='nav-button'>Scans</a><div class='coming-soon'>&nbsp;</div></div>
            <div class='nav-button-wrap'><a href='hide.html' class='nav-button'>Hidden Sketchbooks</a><div class='coming-soon'>&nbsp;</div></div>
            <div class='nav-button-wrap'><div class='nav-button nav-button-soon'>Admin Tools</div><div class='coming-soon'>coming soon</div></div>
        </div>
        @@ERRORSECTION@@
    </div>");

    html.AppendLine(@"
    <script>
    (function() {
        var STORED_HASH = '@@HASH@@';
        var STORE_KEY = '@@COOKIE@@';

        // localStorage is used (not a cookie) because browsers refuse to persist
        // cookies on file:// pages, which broke remember-me during local testing.
        // localStorage persists on both file:// and the live HTTPS site.
        function getStored() {
            try { return localStorage.getItem(STORE_KEY); } catch (e) { return null; }
        }
        function setStored(val) {
            try { localStorage.setItem(STORE_KEY, val); } catch (e) { /* ignore */ }
        }
        function clearStored() {
            try { localStorage.removeItem(STORE_KEY); } catch (e) { /* ignore */ }
        }
        async function sha256hex(s) {
            var buf = await crypto.subtle.digest('SHA-256', new TextEncoder().encode(s));
            return Array.from(new Uint8Array(buf)).map(function(b) {
                return b.toString(16).padStart(2, '0');
            }).join('');
        }

        var gate = document.getElementById('admin-gate');
        var content = document.getElementById('admin-content');
        var form = document.getElementById('admin-gate-form');
        var pass = document.getElementById('admin-pass');
        var err = document.getElementById('admin-gate-error');
        var logout = document.getElementById('admin-logout');

        function unlock() {
            gate.style.display = 'none';
            content.style.display = '';
        }
        function lock() {
            clearStored();
            content.style.display = 'none';
            gate.style.display = '';
            pass.value = '';
            pass.focus();
        }

        // Already unlocked? Storage holds the password hash; if it matches the
        // stored hash, reveal the page without asking again.
        if (getStored() === STORED_HASH) {
            unlock();
        }

        form.addEventListener('submit', async function(e) {
            e.preventDefault();
            err.textContent = '';
            var hash;
            try {
                hash = await sha256hex(pass.value);
            } catch (ex) {
                err.textContent = 'Unable to hash password in this browser.';
                return;
            }
            if (hash === STORED_HASH) {
                setStored(hash);
                unlock();
            } else {
                err.textContent = 'Incorrect password.';
                pass.select();
            }
        });

        if (logout) logout.addEventListener('click', lock);
    })();
    </script>");

    // Render the live error summary from this run below the buttons. When generated
    // without a run (static-only mode), there are no counts to show.
    string errorSection;
    if (errorSummary != null && errorSummary.Count > 0)
    {
      var body = string.Join("\n", errorSummary.Select(EscapeHtml));
      errorSection =
        "<div class='admin-section'>\n" +
        "            <h2>Last generation &mdash; error summary</h2>\n" +
        $"            <pre class='admin-errors'>{body}</pre>\n" +
        "        </div>";
    }
    else
    {
      errorSection =
        "<div class='admin-section'>\n" +
        "            <h2>Last generation &mdash; error summary</h2>\n" +
        "            <p>Error counts are available after a full generation.</p>\n" +
        "        </div>";
    }

    html.Replace("@@HASH@@", AdminPasswordHash);
    html.Replace("@@COOKIE@@", AdminCookieName);
    html.Replace("@@ERRORSECTION@@", errorSection);
    html.AppendLine(GetHtmlFooter());

    await File.WriteAllTextAsync(Path.Combine(_outputDirectory, "admin.html"), html.ToString());
  }
}
