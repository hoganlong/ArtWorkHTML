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
  // -----------------------------------------------------------------------

  public ArtworkHTML(string connectionString, string outputDirectory)
  {
    _connectionString = connectionString;
    _outputDirectory = outputDirectory;
  }

  public async Task GenerateAllPages()
  {
    await GenerateIndexPage();
    await GenerateStatisticsPage();
    await GenerateArtworkPages();
    await GenerateStylesheet();

    Console.WriteLine("  ✓ index.html - Landing page");
    Console.WriteLine("  ✓ statistics.html - Archive statistics");
    Console.WriteLine("  ✓ artworksplus.html - Complete artwork list");
    Console.WriteLine("  ✓ style.css - Stylesheet");
  }

  private async Task GenerateIndexPage()
  {
    var html = new StringBuilder();
    html.AppendLine(GetHtmlHeader("Keith Long Archive"));
    html.AppendLine(@"
    <div class='container landing-page'>
        <div class='landing-header'>
            <h1>Keith Long Archive</h1>
            <p class='subtitle'>Digital Archive of Artwork Collection</p>
        </div>

        <div class='navigation'>
            <a href='artworksplus.html' class='nav-button'>🖼️ Browse All Artworks</a>
            <a href='polaroids.html' class='nav-button'>🖼️ polaroids</a>
            <a href='sketchbook1.html' class='nav-button'>🖼️ Sketchbooks</a>
            <div class='break-point'></div>
            <a href='statistics.html' class='nav-button'>📊 Archive Statistics</a>
        </div>
    </div>");
    html.AppendLine(GetHtmlFooter());

    await File.WriteAllTextAsync(Path.Combine(_outputDirectory, "index.html"), html.ToString());
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

  private async Task GenerateStylesheet()
  {
    var css = @"
* {
    margin: 0;
    padding: 0;
    box-sizing: border-box;
}

body {
    font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Oxygen, Ubuntu, sans-serif;
    line-height: 1.6;
    color: #333;
    background: #f5f5f5;
}

.container {
    max-width: 80%;
    margin: 0 auto;
    padding: 20px;
}

.oldcontainer {
    max-width: 1400px;
    margin: 0 auto;
    padding: 20px;
}

h1 {
    font-size: 2.5em;
    margin-bottom: 10px;
    color: #2c3e50;
}

.subtitle {
    color: #7f8c8d;
    margin-bottom: 30px;
}

.subtitle a {
    color: #3498db;
    text-decoration: none;
}

.subtitle a:hover {
    text-decoration: underline;
}

.stats-grid {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
    gap: 20px;
    margin: 40px 0;
}

.stat-card {
    background: white;
    padding: 30px;
    border-radius: 8px;
    box-shadow: 0 2px 4px rgba(0,0,0,0.1);
    text-align: center;
}

.stat-number {
    font-size: 2.5em;
    font-weight: bold;
    color: #3498db;
    margin-bottom: 10px;
}

.stat-label {
    color: #7f8c8d;
    font-size: 0.9em;
}

.stats-details {
    margin: -20px 0 30px 0;
}

.stats-details summary {
    cursor: pointer;
    color: #3498db;
    font-size: 0.9em;
    padding: 4px 0;
    user-select: none;
}

.stats-details summary:hover {
    text-decoration: underline;
}

.stats-tab-bar {
    display: flex;
    gap: 6px;
    margin: 12px 0 0 0;
}

.stats-tab {
    padding: 5px 14px;
    border: 1px solid #ccc;
    border-radius: 4px;
    background: #f5f5f5;
    cursor: pointer;
    font-size: 0.85em;
    color: #555;
}

.stats-tab:hover {
    background: #e8e8e8;
}

.stats-tab.active {
    background: #3498db;
    border-color: #3498db;
    color: white;
}

.stats-table {
    width: auto;
    border-collapse: collapse;
    margin: 12px auto 0 auto;
    font-size: 0.9em;
}

.stats-table th {
    background: #f0f4f8;
    text-align: left;
    padding: 8px 12px;
    border-bottom: 2px solid #dde3ea;
    font-weight: 600;
    color: #555;
}

.stats-table td {
    padding: 6px 12px;
    border-bottom: 1px solid #ccc;
}

.stats-table tr:last-child td {
    border-bottom: none;
}

.stats-table tr:hover td {
    background: #f9fbfd;
}

.navigation {
    display: flex;
    gap: 15px;
    margin: 40px 0;
    flex-wrap: wrap;
}

.nav-button {
    display: inline-block;
    padding: 15px 30px;
    background: #3498db;
    color: white;
    text-decoration: none;
    border-radius: 5px;
    font-weight: 500;
    transition: background 0.3s;
}

.break-point {
  flex-basis: 100%; /* Forces the next element onto a new line */
  height: 0; /* Keeps the break element from taking up vertical space */
}

.nav-button:hover {
    background: #2980b9;
}

.nav-button-sm {
    padding: 7px 16px;
    font-size: 0.85em;
}

.nav-button.active {
    background: #2c3e50;
    cursor: default;
}

.sketchbook-nav {
    display: flex;
    align-items: center;
    flex-wrap: wrap;
    gap: 5px;
    margin: 8px 0;
}

.sketchbook-nav-label {
    font-weight: bold;
    font-size: 0.85em;
    color: #555;
    margin-right: 4px;
}

.sketchbook-nav-button {
    padding: 4px 10px;
    font-size: 0.8em;
}

.artwork-table {
    width: 100%;
    background: white;
    border-radius: 8px;
    overflow: hidden;
    box-shadow: 0 2px 4px rgba(0,0,0,0.1);
}

.artwork-table thead {
    background: #34495e;
    color: white;
}

.artwork-table th {
    padding: 15px;
    text-align: left;
    font-weight: 600;
}

.artwork-table td {
    padding: 12px 15px;
    border-bottom: 1px solid #ecf0f1;
}

.artwork-table tbody tr:hover {
    background: #f8f9fa;
}

.id-cell {
    font-family: 'Courier New', monospace;
    color: #7f8c8d;
    font-size: 0.9em;
}

.title-cell {
    font-weight: 500;
    color: #2c3e50;
}

.date-cell {
    white-space: nowrap;
}

.dimensions-cell {
    font-size: 0.9em;
    color: #7f8c8d;
}

footer {
    text-align: center;
    padding: 40px 20px;
    color: #7f8c8d;
    font-size: 0.9em;
}

  div.gallery 
  {
    display: flex;
    flex-wrap: wrap;
    justify-content: flex-start;
  }
  div.gallery-item
  {
    margin: 5px;
    border: 1px solid #ccc;
    width: 250px;
    position: relative;
  }
  div.gallery-item:hover
  {
    border: 1px solid #777;
    z-index: 50;
  }
  div.gallery-item img
  {
    width: 100%;
    height: auto;
  }
  div.gallery-item > a > img
  {
    display: block;
    transition: transform 0.2s ease;
  }
  div.gallery-item > a > img:hover
  {
    transform: scale(2);
    transform-origin: bottom center;
  }
  div.gallery-item div.desc
  {
    padding: 5px;
    text-align: center;
  }

  .thumb-buttons {
    display: flex;
    justify-content: center;
    gap: 5px;
    padding: 5px;
    background: #f8f9fa;
  }

  .thumb-button {
    display: inline-block;
    position: relative;
    border: 2px solid #ccc;
    border-radius: 4px;
    transition: border-color 0.2s;
  }

  .thumb-button:hover {
    border-color: #3498db;
  }

  .thumb-button img:not(.thumb-preview) {
    display: block;
    width: 36px;
    height: 36px;
  }

  .thumb-button img.thumb-preview {
    display: none;
    position: absolute;
    bottom: 44px;
    left: 50%;
    transform: translateX(-50%);
    max-width: 280px;
    max-height: 280px;
    width: auto;
    height: auto;
    border: 2px solid #3498db;
    border-radius: 4px;
    box-shadow: 0 4px 16px rgba(0,0,0,0.45);
    background: white;
    z-index: 200;
    pointer-events: none;
  }

  .thumb-button:hover img.thumb-preview {
    display: block;
  }

  body.no-thumb-hover .thumb-button:hover img.thumb-preview {
    display: none;
  }

  body.no-image-hover div.gallery-item > a > img:hover {
    transform: none;
  }

  .site-notice {
    text-align: center;
    background: #0000CC;
    color: #ffffff;
    padding: 10px 20px;
    font-size: 0.85em;
    border-bottom: 1px solid #d0c8b8;
    letter-spacing: 0.02em;
  }

  .page-controls {
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 4px;
    padding: 6px 16px;
    background: #f0f4f8;
    border-bottom: 1px solid #d0d8e0;
    font-size: 0.875em;
    color: #444;
  }

  .page-controls-row {
    display: flex;
    align-items: center;
    flex-wrap: wrap;
    justify-content: center;
    gap: 4px 14px;
  }

  .page-controls-label {
    font-weight: bold;
    color: #555;
  }

  .page-controls label {
    cursor: pointer;
    display: flex;
    align-items: center;
    gap: 4px;
    white-space: nowrap;
  }

  #type-filter-checkboxes {
    display: inline-flex;
    flex-wrap: wrap;
    gap: 2px 10px;
    align-items: center;
  }

@media (max-width: 768px) {
    .artwork-table {
        font-size: 0.9em;
    }

    .artwork-table th, .artwork-table td {
        padding: 8px 10px;
    }

    h1 {
        font-size: 2em;
    }
}
";

    await File.WriteAllTextAsync(Path.Combine(_outputDirectory, "style.css"), css);
  }

  private string GetHtmlHeader(string title)
  {
    return $@"<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>{EscapeHtml(title)}</title>
    <link rel='stylesheet' href='style.css'>
</head>
<body>
<div class='site-notice'>
    &#128274; This website is <strong>not open to the public</strong> &mdash; it is in development.
    All images and content are &copy; Estate of Keith Long.
</div>
  ";
  }

  private string GetHtmlFooter()
  {
    return $@"
    <footer>
        <p>Keith Long Archive | Generated {DateTime.Now:MMMM d, yyyy}</p>
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
