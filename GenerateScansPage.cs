using System.Text;

namespace ArtWorkHTML;

public partial class ArtworkHTML
{
  // Prefix buckets for unmatched scans (2026-10-06): each (Label, Prefix) pairs a
  // button on scanslist.html with its own page. Prefix includes the trailing "_"
  // from the naming convention "<PREFIX>_<unique#>" (or "Walk_<year>_<unique#>"),
  // so matching is a plain, unambiguous StartsWith — no prefix here is a prefix of
  // another's literal string (e.g. "WOP_" is never a prefix-match for "WOPP_" or
  // vice versa, because the character right after the shared letters differs: '_'
  // vs the next letter). FileBase is the generated page's filename (without ".html").
  // Some of these may end up empty once those filenames are added to the artwork/
  // photo DB tables — that's expected, not a bug.
  private static readonly (string Label, string Prefix, string FileBase)[] ScanPrefixes =
  {
    ("CP",   "CP_",   "scans-cp"),
    ("SOB",  "SOB_",  "scans-sob"),
    ("SGT",  "SGT_",  "scans-sgt"),
    ("SIX",  "SIX_",  "scans-six"),
    ("LLD",  "LLD_",  "scans-lld"),
    ("MISC", "MISC_", "scans-misc"),
    ("FIG",  "FIG_",  "scans-fig"),
    ("ILL",  "ILL_",  "scans-ill"),
    ("LWB",  "LWB_",  "scans-lwb"),
    ("AWP",  "AWP_",  "scans-awp"),
    ("PRI",  "PRI_",  "scans-pri"),
    ("FRAM", "FRAM_", "scans-fram"),
    ("CLG",  "CLG_",  "scans-clg"),
    ("LGT",  "LGT_",  "scans-lgt"),
    ("HS",   "HS_",   "scans-hs"),
    ("SD",   "SD_",   "scans-sd"),
    ("AWB",  "AWB_",  "scans-awb"),
    ("MGT",  "MGT_",  "scans-mgt"),
    ("LCP",  "LCP_",  "scans-lcp"),
    ("WO",   "WO_",   "scans-wo"),
    ("HSP",  "HSP_",  "scans-hsp"),
    ("HSB",  "HSB_",  "scans-hsb"),
    ("HSD",  "HSD_",  "scans-hsd"),
    ("PC",   "PC_",   "scans-pc"),
    ("DC",   "DC_",   "scans-dc"),
    ("WC",   "WC_",   "scans-wc"),
    ("PAC",  "PAC_",  "scans-pac"),
    ("SIXX", "SIXX_", "scans-sixx"),
    ("WOP",  "WOP_",  "scans-wop"),
    ("WOPP", "WOPP_", "scans-wopp"),
    ("NTS",  "NTS_",  "scans-nts"),
    ("WB",   "WB_",   "scans-wb"),
    ("WW",   "WW_",   "scans-ww"),
    ("DIF",  "DIF_",  "scans-dif"),
    ("HDP",  "HDP_",  "scans-hdp"),
    ("PACO", "PACO_", "scans-paco"),
    ("Walk", "Walk_", "scans-walk"),
    ("EXHI", "EXHI_", "scans-exhi"),
    ("LA",   "LA_",   "scans-la"),
    ("ALCP", "ALCP_", "scans-alcp"),
  };

  // Splits scansList by ScanPrefixes (matched against Artwork.fileName, case-
  // insensitive), generates one page per matching prefix plus an "Other" page
  // (kept at the original "scans.html" name) for anything left over, then the
  // scanslist.html hub linking to all of them. Replaces the single flat
  // scans.html this used to be (see GenerateScansListPage for the hub).
  private async Task GenerateScansPage(ArtList scansList)
  {
    var remaining = new List<Artwork>(scansList.artworks.Values);
    var counts = new List<(string Label, string FileBase, int Count)>();

    foreach (var (label, prefix, fileBase) in ScanPrefixes)
    {
      var matched = remaining
        .Where(a => a.fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        .ToList();
      remaining.RemoveAll(a => matched.Contains(a));
      await GenerateScansSubPage(matched, $"Scans - {label}", $"{fileBase}.html");
      counts.Add((label, fileBase, matched.Count));
    }

    // Everything no prefix claimed — the page that used to be all of scans.html.
    await GenerateScansSubPage(remaining, "Scans - Other", "scans.html");

    await GenerateScansListPage(remaining.Count, counts);
  }

  private async Task GenerateScansSubPage(List<Artwork> items, string title, string fileName)
  {
    var html = new StringBuilder();
    html.AppendLine(GetHtmlHeader($"{title} - Keith Long Archive", canonicalPath: fileName, noindex: true));

    html.AppendLine($@"
    <div class='container'>
        <h1>{EscapeHtml(title)}</h1>
        <p class='subtitle'><a id='back-link' href='scanslist.html'>← Back to Scans List</a></p>
    </div>
    <div class='page-controls'>
        <span class='page-controls-label'>Hover effects:</span>
        <label><input type='checkbox' id='chk-image-hover' checked onchange='document.body.classList.toggle(""no-image-hover"", !this.checked)'> Image zoom (z)</label>
    </div>");
    html.AppendLine(SharedScriptTag(TagsScriptFile));
    html.AppendLine(SharedScriptTag(KeysScriptFile));
    html.AppendLine(@"
    <div id='tag-title' class='tag-title-banner' style='display:none'></div>
    <div class='container'>");

    html.AppendLine("<div class='gallery' style='font-size: x-small;'>");

    foreach (var art in items.OrderBy(a => a.fileName))
    {
      // No baked-in tag-active here: visibility is left to tags.js so the date filter
      // works, e.g. scans-mgt.html?show=d:8/27/2026 for everything uploaded that day. A
      // bare URL still shows every item, since no filter at all means show all. The date
      // is the S3 object's LastModified (the same one displayed below), so on this page
      // the filter reads as "when was this file uploaded".
      html.AppendLine($@"<div class='gallery-item'{DateAttr(art.ctDate)}>");

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

    await File.WriteAllTextAsync(Path.Combine(_outputDirectory, fileName), html.ToString());
  }

  // The hub scanslist.html replaces scans.html as admin.html's "Scans" button
  // target: one button per ScanPrefixes entry (each showing its current count, so
  // an empty page — e.g. once its filenames are all added to the artwork/photo DB
  // tables — is visible at a glance, not a trap) plus "Other" for whatever no
  // prefix claimed.
  private async Task GenerateScansListPage(int otherCount, List<(string Label, string FileBase, int Count)> counts)
  {
    var html = new StringBuilder();
    html.AppendLine(GetHtmlHeader("Scans List - Keith Long Archive", canonicalPath: "scanslist.html", noindex: true));

    html.AppendLine(@"
    <div class='container landing-page'>
        <div class='landing-header'>
            <h1>Scans List</h1>
            <p class='subtitle'><a id='back-link' href='admin.html'>← Back to Admin</a></p>
        </div>
        <div class='navigation'>");

    html.AppendLine($@"            <div class='nav-button-wrap'><a href='scans.html' class='nav-button'>Other ({otherCount})</a><div class='coming-soon'>&nbsp;</div></div>");
    foreach (var (label, fileBase, count) in counts)
      html.AppendLine($@"            <div class='nav-button-wrap'><a href='{fileBase}.html' class='nav-button'>{EscapeHtml(label)} ({count})</a><div class='coming-soon'>&nbsp;</div></div>");

    html.AppendLine(@"
        </div>
    </div>");
    html.AppendLine(GetHtmlFooter());

    await File.WriteAllTextAsync(Path.Combine(_outputDirectory, "scanslist.html"), html.ToString());
  }
}
