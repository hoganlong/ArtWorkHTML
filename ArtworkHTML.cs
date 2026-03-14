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

  public async Task GenerateStaticPages()
  {
    await GenerateIndexPage();
    await GenerateCopyrightPage();
    await GenerateHowIsMadePage();
    await GenerateCreditsPage();
    await GenerateHelpPage();
    await GenerateFeedbackPage();
    await GenerateStylesheet();

    Console.WriteLine("  ✓ index.html - Landing page");
    Console.WriteLine("  ✓ copyright.html");
    Console.WriteLine("  ✓ howisitmade.html");
    Console.WriteLine("  ✓ credits.html");
    Console.WriteLine("  ✓ help.html");
    Console.WriteLine("  ✓ feedback.html");
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
    await GenerateStylesheet();

    Console.WriteLine("  ✓ index.html - Landing page");
    Console.WriteLine("  ✓ statistics.html - Archive statistics");
    Console.WriteLine("  ✓ artworksplus.html - Complete artwork list");
    Console.WriteLine("  ✓ copyright.html");
    Console.WriteLine("  ✓ howisitmade.html");
    Console.WriteLine("  ✓ credits.html");
    Console.WriteLine("  ✓ help.html");
    Console.WriteLine("  ✓ feedback.html");
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
        <nav class='footer-nav'>
            <a href='copyright.html'>Copyright (can I copy?) details</a>
            <a href='howisitmade.html'>How it is made</a>
            <a href='credits.html'>Credits &amp; thanks</a>
            <a href='help.html'>Help</a>
            <a href='feedback.html'>Feedback</a>
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
