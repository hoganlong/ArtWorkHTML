#pragma warning disable CA2249 

using Microsoft.Extensions.Configuration;
using Npgsql;
using System.Reflection;
using System.Text;

using Amazon.S3;
using Amazon.S3.Model;
using System.Security.Cryptography.X509Certificates;


namespace ArtWorkHTML;

using System;
using System.Collections.Generic;

// Extension methods must be defined in a static class
public static class DictionaryExtensions
{
    /// <summary>
    /// Gets the value associated with the specified key, or creates and adds a new value if the key does not exist.
    /// </summary>
    /// <typeparam name="TKey">The type of the keys in the dictionary.</typeparam>
    /// <typeparam name="TValue">The type of the values in the dictionary.</typeparam>
    /// <param name="dictionary">The dictionary to extend.</param>
    /// <param name="key">The key of the value to get or add.</param>
    /// <returns>The value associated with the key.</returns>
    public static TValue GetOrCreate<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key)
        where TValue : new() // Constrains TValue to have a parameterless constructor
    {
        if (!dictionary.TryGetValue(key, out TValue? ret) || ret == null) // A safer way to access values than using the indexer directly
        {
            ret = new TValue();
            dictionary[key] = ret;
        }
        return ret!;
    }
}


public static class NullExtensions
{
    public static IEnumerable<T> NotNull<T>(this IEnumerable<T?> enumerable) where T : class
    {
        return enumerable.Where(e => e != null).Select(e => e!);
    }
}

public partial class ArtworkHTML
{
  private readonly string _connectionString;
  private readonly string _outputDirectory;
  private readonly Dictionary<string, int> _errorCounts = new();
  private int _polaroidCount = 0;
  private int _scansCount = 0;
  private int _dateNotEnteredCount = 0;
  private int _dateUnknownCount = 0;
  public bool DbSketchOnly { get; set; } = false;
  private static readonly string _version =
    System.Reflection.Assembly.GetExecutingAssembly()
      .GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>()
      ?.InformationalVersion.Split('+')[0] ?? "";

  // -----------------------------------------------------------------------
  // Type code descriptions — update the values here to replace placeholders
  // -----------------------------------------------------------------------
  private static readonly Dictionary<string, string> TypeDescriptions = new(StringComparer.OrdinalIgnoreCase)
  {
    {"W",	"Wall hanging sculpture"},
    {"D",	"Drawing"},
    {"S",	"Sculpture (non-wall)"},
    {"C",	"Canvas"},
    {"J",	"Jewelry"},
    {"P",	"Painting (non-canvas)"},
    {"B",	"Broach"},
    {"N",	"Necklace"}
    // Add entries as needed; any code not listed falls back to "Description {code}"
  };

  private string GetTypeDescription(string? typeCode)
  {
    if (string.IsNullOrEmpty(typeCode)) return "";
    return TypeDescriptions.TryGetValue(typeCode, out var desc) ? desc : $"Description {typeCode}";
  }

  private static readonly Dictionary<string, string> TypeTags = new(StringComparer.OrdinalIgnoreCase)
  {
    {"W", "Wall-Hanging-Sculpture"},
    {"D", "Drawing"},
    {"S", "Sculpture-NonWall"},
    {"C", "Canvas"},
    {"J", "Jewelry"},
    {"P", "Painting-NonCanvas"},
    {"B", "Broach"},
    {"N", "Necklace"}
  };

  private string GetTypeTag(string? typeCode)
  {
    if (string.IsNullOrEmpty(typeCode)) return "none";
    return TypeTags.TryGetValue(typeCode, out var tag) ? tag : "none";
  }

  // Per-type artwork pages — add an entry here to generate a new filtered
  // page (e.g. artwork-canvas.html). TypeCodes lists which artwork.type codes
  // feed into the page; multiple codes are combined onto one page.
  private record ArtworkTypePage(string FileName, string Title, string[] TypeCodes);

  private static readonly List<ArtworkTypePage> ArtworkTypePages = new()
  {
    new("artwork-canvas.html",             "Canvas",                 new[] { "C" }),
    new("artwork-drawing.html",            "Drawing",                new[] { "D" }),
    new("artwork-jewelry.html",            "Jewelry",                new[] { "B", "N", "J" }),
    new("artwork-painting-noncanvas.html", "Painting (Non-Canvas)",  new[] { "P" }),
    new("artwork-sculpture-nonwall.html",  "Sculpture (Non-Wall)",   new[] { "S" }),
    new("artwork-wall-sculpture.html",     "Wall Hanging Sculptures", new[] { "W" }),
  };

  private static string MakeTag(string? s)
  {
    if (string.IsNullOrEmpty(s)) return "";
    return s.Replace(" ", "-").Replace(",", "").Replace("'", "").Replace("\"", "").Trim('-');
  }

  private static string GetTagsScript()
  {
    return @"(function() {
  var activeTags = new Set();
  var params = new URLSearchParams(window.location.search);
  (params.get('tag') || '').split(',').forEach(function(t) { t=t.trim().toLowerCase(); if(t) activeTags.add(t); });
  (params.get('show') || '').split(',').forEach(function(t) { t=t.trim().toLowerCase(); if(t) activeTags.add(t); });
  var tagtitleValues = (params.get('tagtitle') || '').split(',').map(function(t){ return t.trim(); }).filter(function(t){ return t; });
  tagtitleValues.forEach(function(t) { activeTags.add(t.toLowerCase()); });
  params.forEach(function(val, key) { if(val.toLowerCase()==='true') activeTags.add(key.toLowerCase()); });
  var hash = window.location.hash.replace('#','').trim().toLowerCase();
  if(hash) activeTags.add(hash);
  var cookieMatch = document.cookie.match(/(?:^|;\s*)TAGS=([^;]*)/);
  if(cookieMatch) cookieMatch[1].split(',').forEach(function(t){ t=t.trim().toLowerCase(); if(t) activeTags.add(t); });
  var hasAll = activeTags.has('all');
  window._tagState = { activeTags: activeTags, hasAll: hasAll };
  var back = params.get('back');
  var backlabel = params.get('backlabel');
  if (back || backlabel) {
    document.addEventListener('DOMContentLoaded', function() {
      var link = document.getElementById('back-link');
      if (link) {
        if (back) link.href = back;
        if (backlabel) link.textContent = '\u2190 ' + backlabel;
      }
    });
  }
  document.addEventListener('DOMContentLoaded', function() {
    var titleEl = document.getElementById('page-title');
    var banner = document.getElementById('tag-title');
    if (tagtitleValues.length > 0) {
      if (banner) { banner.textContent = tagtitleValues.join(' & '); banner.style.display = ''; }
      if (titleEl) titleEl.textContent = 'Artwork - ' + tagtitleValues.join(' & ');
      var rows = document.querySelectorAll('.page-controls-row');
      var hoverRow = null;
      rows.forEach(function(row) {
        if (row.querySelector('#type-filter-checkboxes')) row.style.display = 'none';
        if (row.querySelector('#chk-image-hover')) hoverRow = row;
      });
      var pageControls = document.querySelector('.page-controls');
      if (pageControls) pageControls.style.display = 'none';
      if (hoverRow && banner) banner.insertAdjacentElement('afterend', hoverRow);
    } else if (activeTags.size > 0 && !hasAll) {
      if (titleEl) titleEl.textContent = 'Artwork';
    }
  });
  function updateSeriesButtons() {
    document.querySelectorAll('.series-tag-btn').forEach(function(btn) {
      var tag = (btn.getAttribute('data-series-tag') || '').toLowerCase();
      btn.style.display = (tag && activeTags.has(tag)) ? 'none' : '';
    });
  }
  document.addEventListener('DOMContentLoaded', function() {
    document.querySelectorAll('.gallery-item').forEach(function(item) {
      if(hasAll) { item.classList.add('tag-active'); return; }
      var allTagText = Array.from(item.querySelectorAll('my-tags, my-hidden-tags')).map(function(el){ return el.textContent; }).join(',');
      var itemTags = allTagText.split(',').map(function(t){ return t.trim().toLowerCase(); }).filter(function(t){ return t; });
      for(var i=0; i<itemTags.length; i++) {
        if(activeTags.has(itemTags[i])) { item.classList.add('tag-active'); break; }
      }
    });
    updateSeriesButtons();
  });
  window._filterToTag = function(tag) {
    tag = tag.toLowerCase();
    activeTags.clear();
    activeTags.add(tag);
    hasAll = false;
    document.querySelectorAll('.gallery-item').forEach(function(item) {
      item.classList.remove('tag-active');
      var allTagText = Array.from(item.querySelectorAll('my-tags, my-hidden-tags')).map(function(el){ return el.textContent; }).join(',');
      var itemTags = allTagText.split(',').map(function(t){ return t.trim().toLowerCase(); }).filter(function(t){ return t; });
      for(var i=0; i<itemTags.length; i++) {
        if(activeTags.has(itemTags[i])) { item.classList.add('tag-active'); break; }
      }
    });
    updateSeriesButtons();
  };
})();";
  }
  // -----------------------------------------------------------------------

  public ArtworkHTML(string connectionString, string outputDirectory)
  {
    _connectionString = connectionString;
    _outputDirectory = outputDirectory;
  }

  private List<string> BuildErrorSummaryLines()
  {
    var lines = new List<string>
    {
      $"=== Artwork Page Errors | Generated {DateTime.Now:MMMM d, yyyy' at 'h:mm tt} | v{_version} ==="
    };
    foreach (var kvp in _errorCounts.OrderByDescending(x => x.Value))
      lines.Add($"  {kvp.Value,4}x  {kvp.Key}");
    lines.Add($"  {_polaroidCount,5}  Unmatched polaroids");
    lines.Add($"  {_scansCount,5}  Scans without art type");
    lines.Add($"  {_dateNotEnteredCount,5}  date not entered (year 1899)");
    lines.Add($"  {_dateUnknownCount,5}  date unknown (year 1900)");
    return lines;
  }

  private void CleanOutputDirectory()
  {
    foreach (var file in Directory.EnumerateFiles(_outputDirectory, "*.html", SearchOption.AllDirectories))
      File.Delete(file);
    foreach (var file in Directory.EnumerateFiles(_outputDirectory, "*.css", SearchOption.AllDirectories))
      File.Delete(file);
    foreach (var file in Directory.EnumerateFiles(_outputDirectory, "*.js", SearchOption.AllDirectories))
      File.Delete(file);
  }

  private async Task GenerateLightboxScript()
  {
    await File.WriteAllTextAsync(Path.Combine(_outputDirectory, "lightbox.js"), GetLightboxScript());
  }

  private static string GetLightboxScriptTag(string pathPrefix = "")
  {
    return $"<script src='{pathPrefix}lightbox.js'></script>";
  }

  public async Task GenerateStaticPages()
  {
    CleanOutputDirectory();
    await GenerateIndexPage();
    await GenerateCopyrightPage();
    await GenerateHowIsMadePage();
    await GenerateCreditsPage();
    await GenerateHelpPage(null);
    await GenerateFeedbackPage();
    await GenerateOpensourcePage();
    await GenerateStylesheet();
    await GenerateLightboxScript();

    Console.WriteLine("  ✓ index.html - Landing page");
    Console.WriteLine("  ✓ copyright.html");
    Console.WriteLine("  ✓ howisitmade.html");
    Console.WriteLine("  ✓ credits.html");
    Console.WriteLine("  ✓ help.html");
    Console.WriteLine("  ✓ feedback.html");
    Console.WriteLine("  ✓ opensource.html");
    Console.WriteLine("  ✓ style.css - Stylesheet");
    Console.WriteLine("  ✓ lightbox.js - Lightbox script");
  }

  public async Task GenerateAllPages()
  {
    CleanOutputDirectory();
    await GenerateIndexPage();
    await GenerateStatisticsPage();
    await GenerateArtworkPages();
    await GenerateShowsPage();
    await GenerateCopyrightPage();
    await GenerateHowIsMadePage();
    await GenerateCreditsPage();

    var years = new List<int>();
    var yearSql = "SELECT DISTINCT EXTRACT(YEAR FROM create_dt)::int FROM artwork WHERE create_dt IS NOT NULL AND EXTRACT(YEAR FROM create_dt) != 1900 ORDER BY 1";
    await using (var conn = new NpgsqlConnection(_connectionString))
    {
      await conn.OpenAsync();
      await using var cmd = new NpgsqlCommand(yearSql, conn);
      await using var reader = await cmd.ExecuteReaderAsync();
      while (await reader.ReadAsync())
        years.Add(reader.GetInt32(0));
    }

    var sketchPages = new Dictionary<int, List<int>>();
    var sketchPageSql = "SELECT DISTINCT sketchbook_number, page_number FROM sketch WHERE (hide IS NOT TRUE) AND sketchbook_number IS NOT NULL AND page_number IS NOT NULL ORDER BY sketchbook_number, page_number";
    await using (var conn2 = new NpgsqlConnection(_connectionString))
    {
      await conn2.OpenAsync();
      await using var cmd2 = new NpgsqlCommand(sketchPageSql, conn2);
      await using var reader2 = await cmd2.ExecuteReaderAsync();
      while (await reader2.ReadAsync())
      {
        int bookNum = reader2.GetInt32(0);
        int pageNum = reader2.GetInt32(1);
        if (!sketchPages.ContainsKey(bookNum))
          sketchPages[bookNum] = new List<int>();
        sketchPages[bookNum].Add(pageNum);
      }
    }
    await GenerateHelpPage(years, sketchPages);
    await GenerateFeedbackPage();
    await GenerateOpensourcePage();
    await GenerateStylesheet();
    await GenerateLightboxScript();

    Console.WriteLine("  ✓ index.html - Landing page");
    Console.WriteLine("  ✓ statistics.html - Archive statistics");
    Console.WriteLine("  ✓ artwork.html - Complete artwork list");
    Console.WriteLine("  ✓ scans.html - Scan files not in database");
    Console.WriteLine("  ✓ copyright.html");
    Console.WriteLine("  ✓ howisitmade.html");
    Console.WriteLine("  ✓ credits.html");
    Console.WriteLine("  ✓ help.html");
    Console.WriteLine("  ✓ feedback.html");
    Console.WriteLine("  ✓ opensource.html");
    Console.WriteLine("  ✓ style.css - Stylesheet");
    Console.WriteLine("  ✓ lightbox.js - Lightbox script");

    Console.WriteLine();
    foreach (var line in BuildErrorSummaryLines())
      Console.WriteLine(line);
  }


  private static string BlankOrComment(string inS, string prepend = "")
  {
    if (!string.IsNullOrEmpty(inS))
    {
      return ("<!--" + prepend + inS + "-->");
    }
    else
      return ("");
  }

  private static string DateOrEmpty(DateTime dt) =>
    (dt == DateTime.MinValue || dt.Year == 1900) ? "" : dt.ToShortDateString();

  private static string BlankOrWithBR(string inS, string prepend = "")
  {
    if (!string.IsNullOrEmpty(inS))
    {
      return (prepend + inS + "<br/>");
    }
    else
      return ("");
  }

  private static string BlankOrWithBR(string inS, string prepend = "", string append = "")
  {
    if (!string.IsNullOrEmpty(inS))
    {
      return (prepend + inS + append + "<br/>");
    }
    else
      return ("");
  }

  private string GetHtmlHeader(string title, string pathPrefix = "")
  {
    return $@"<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>{EscapeHtml(title)}</title>
    <link rel='icon' type='image/png' href='/favicon.png'>
    <link rel='stylesheet' href='{pathPrefix}style.css'>
</head>
<body>
<div class='site-notice'>
    &#128274; This website is <strong>not open to the public</strong> &mdash; it is in development.
    All images and content are &copy; Estate of Keith Long.
</div>
  ";
  }

  private string GetHtmlFooter(string pathPrefix = "")
  {
    return $@"
    <footer>
        <nav class='footer-nav'>
            <a href='{pathPrefix}copyright.html'>Copyright (can I copy?) details</a>
            <a href='{pathPrefix}howisitmade.html'>How it is made</a>
            <a href='{pathPrefix}credits.html'>Credits &amp; thanks</a>
            <a href='{pathPrefix}help.html'>Help</a>
            <a href='{pathPrefix}feedback.html'>Feedback</a>
            <a href='{pathPrefix}opensource.html'>Opensource</a>
        </nav>
        <p>Keith Long Archive | Generated {DateTime.Now:MMMM d, yyyy' at 'h:mm tt} | v{_version}</p>
    </footer>
</body>
</html>";
  }

  private string EscapeHtml(string? text)
  {
    if (string.IsNullOrEmpty(text)) return "";
    return text.Replace("&", "&amp;")
               .Replace("<", "&lt;")
               .Replace(">", "&gt;")
               .Replace("\"", "&quot;")
               .Replace("'", "&#39;");
  }

  private static string GetLightboxHtml()
  {
    return @"<div id='lightbox' class='lightbox'>
  <div class='lightbox-overlay'></div>
  <div class='lightbox-container'>
    <button class='lightbox-close' title='Close (Esc)'>&#x2715;</button>
    <button class='lightbox-prev' title='Previous artwork (&#8592;)'>&#10094;</button>
    <div class='lightbox-content'>
      <img class='lightbox-img' src='' alt='' />
      <div class='lightbox-view-nav' style='display:none'>
        <button class='lightbox-view-prev' title='Previous view (&#8593;)'>&#10094;</button>
        <span class='lightbox-view-label'></span>
        <button class='lightbox-view-next' title='Next view (&#8595;)'>&#10095;</button>
      </div>
      <div class='lightbox-caption'></div>
    </div>
    <button class='lightbox-next' title='Next artwork (&#8594;)'>&#10095;</button>
  </div>
</div>";
  }

  private static string GetLightboxScript()
  {
    return @"(function() {
  var lb = document.getElementById('lightbox');
  var lbImg = lb.querySelector('.lightbox-img');
  var lbCaption = lb.querySelector('.lightbox-caption');
  var lbPrev = lb.querySelector('.lightbox-prev');
  var lbNext = lb.querySelector('.lightbox-next');
  var lbClose = lb.querySelector('.lightbox-close');
  var lbViewNav = lb.querySelector('.lightbox-view-nav');
  var lbViewPrev = lb.querySelector('.lightbox-view-prev');
  var lbViewNext = lb.querySelector('.lightbox-view-next');
  var lbViewLabel = lb.querySelector('.lightbox-view-label');
  var items = [], currentIndex = 0;
  var currentViews = [], currentView = 0;

  function getItemViews(item) {
    var views = [];
    var mainAnchor = item.querySelector(':scope > a');
    if (mainAnchor && mainAnchor.href) {
      views.push({ src: mainAnchor.href, label: 'Main' });
    }
    item.querySelectorAll('.thumb-button').forEach(function(btn) {
      if (btn.href) {
        views.push({ src: btn.href, label: btn.title || 'View' });
      }
    });
    return views;
  }

  function openLightbox(item, viewHref) {
    items = Array.from(document.querySelectorAll('.gallery-item.tag-active'));
    currentIndex = items.indexOf(item);
    showItem(currentIndex);
    if (viewHref) {
      var i = currentViews.findIndex(function(v) { return v.src === viewHref; });
      if (i > 0) showView(i);
    }
    lb.classList.add('active');
    document.body.style.overflow = 'hidden';
  }
  function closeLightbox() {
    lb.classList.remove('active');
    document.body.style.overflow = '';
  }
  function showView(viewIndex) {
    currentView = viewIndex;
    lbImg.src = currentViews[currentView].src;
    lbViewLabel.textContent = currentViews[currentView].label + ' (' + (currentView + 1) + '/' + currentViews.length + ')';
    lbViewPrev.style.visibility = currentView > 0 ? '' : 'hidden';
    lbViewNext.style.visibility = currentView < currentViews.length - 1 ? '' : 'hidden';
  }
  function showItem(index) {
    var item = items[index];
    currentViews = getItemViews(item);
    currentView = 0;
    lbImg.src = currentViews.length > 0 ? currentViews[0].src : '';
    if (currentViews.length > 1) {
      lbViewNav.style.display = '';
      lbViewLabel.textContent = currentViews[0].label + ' (1/' + currentViews.length + ')';
      lbViewPrev.style.visibility = 'hidden';
      lbViewNext.style.visibility = '';
    } else {
      lbViewNav.style.display = 'none';
    }
    var descEl = item.querySelector('.item-description');
    lbCaption.innerHTML = descEl ? descEl.innerHTML : '';
    lbPrev.style.visibility = index > 0 ? '' : 'hidden';
    lbNext.style.visibility = index < items.length - 1 ? '' : 'hidden';
  }
  function navigateArtwork(dir) {
    var next = currentIndex + dir;
    if (next >= 0 && next < items.length) { currentIndex = next; showItem(currentIndex); }
  }
  function navigateView(dir) {
    var next = currentView + dir;
    if (next >= 0 && next < currentViews.length) { showView(next); }
  }

  document.addEventListener('click', function(e) {
    if (e.ctrlKey || e.metaKey || e.shiftKey || e.button !== 0) return;
    var thumbAnchor = e.target.closest('.thumb-button');
    if (thumbAnchor) {
      var galleryItem = thumbAnchor.closest('.gallery-item');
      if (galleryItem) {
        e.preventDefault();
        openLightbox(galleryItem, thumbAnchor.href);
        return;
      }
    }
    var anchor = e.target.closest('.gallery-item > a');
    if (!anchor) return;
    e.preventDefault();
    openLightbox(anchor.closest('.gallery-item'));
  });
  lbPrev.addEventListener('click', function() { navigateArtwork(-1); });
  lbNext.addEventListener('click', function() { navigateArtwork(1); });
  lbViewPrev.addEventListener('click', function() { navigateView(-1); });
  lbViewNext.addEventListener('click', function() { navigateView(1); });
  lbClose.addEventListener('click', closeLightbox);
  lb.querySelector('.lightbox-overlay').addEventListener('click', closeLightbox);
  document.addEventListener('keydown', function(e) {
    if (!lb.classList.contains('active')) return;
    if (e.key === 'ArrowLeft') navigateArtwork(-1);
    if (e.key === 'ArrowRight') navigateArtwork(1);
    if (e.key === 'ArrowUp') { e.preventDefault(); navigateView(-1); }
    if (e.key === 'ArrowDown') { e.preventDefault(); navigateView(1); }
    if (e.key === 'Escape') closeLightbox();
  });
})();";
  }
}
