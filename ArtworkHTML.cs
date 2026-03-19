#pragma warning disable CA2249 

using Microsoft.Extensions.Configuration;
using Npgsql;
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
  document.addEventListener('DOMContentLoaded', function() {
    document.querySelectorAll('.gallery-item').forEach(function(item) {
      if(hasAll) { item.classList.add('tag-active'); return; }
      var allTagText = Array.from(item.querySelectorAll('my-tags, my-hidden-tags')).map(function(el){ return el.textContent; }).join(',');
      var itemTags = allTagText.split(',').map(function(t){ return t.trim().toLowerCase(); }).filter(function(t){ return t; });
      for(var i=0; i<itemTags.length; i++) {
        if(activeTags.has(itemTags[i])) { item.classList.add('tag-active'); break; }
      }
    });
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
  };
})();";
  }
  // -----------------------------------------------------------------------

  public ArtworkHTML(string connectionString, string outputDirectory)
  {
    _connectionString = connectionString;
    _outputDirectory = outputDirectory;
  }

  public async Task GenerateStaticPages()
  {
    await GenerateIndexPage();
    await GenerateCopyrightPage();
    await GenerateHowIsMadePage();
    await GenerateCreditsPage();
    await GenerateHelpPage();
    await GenerateFeedbackPage();
    await GenerateOpensourcePage();
    await GenerateStylesheet();

    Console.WriteLine("  ✓ index.html - Landing page");
    Console.WriteLine("  ✓ copyright.html");
    Console.WriteLine("  ✓ howisitmade.html");
    Console.WriteLine("  ✓ credits.html");
    Console.WriteLine("  ✓ help.html");
    Console.WriteLine("  ✓ feedback.html");
    Console.WriteLine("  ✓ opensource.html");
    Console.WriteLine("  ✓ style.css - Stylesheet");
  }

  public async Task GenerateAllPages()
  {
    await GenerateIndexPage();
    await GenerateStatisticsPage();
    await GenerateArtworkPages();
    await GenerateCopyrightPage();
    await GenerateHowIsMadePage();
    await GenerateCreditsPage();
    await GenerateHelpPage();
    await GenerateFeedbackPage();
    await GenerateOpensourcePage();
    await GenerateStylesheet();

    Console.WriteLine("  ✓ index.html - Landing page");
    Console.WriteLine("  ✓ statistics.html - Archive statistics");
    Console.WriteLine("  ✓ artwork.html - Complete artwork list");
    Console.WriteLine("  ✓ copyright.html");
    Console.WriteLine("  ✓ howisitmade.html");
    Console.WriteLine("  ✓ credits.html");
    Console.WriteLine("  ✓ help.html");
    Console.WriteLine("  ✓ feedback.html");
    Console.WriteLine("  ✓ opensource.html");
    Console.WriteLine("  ✓ style.css - Stylesheet");
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

  private static string BlankOrWithBR(string inS, string prepend = "")
  {
    if (!string.IsNullOrEmpty(inS))
    {
      return (prepend + inS + "<br/>");
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
        <p>Keith Long Archive | Generated {DateTime.Now:MMMM d, yyyy' at 'h:mm tt}</p>
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
}
