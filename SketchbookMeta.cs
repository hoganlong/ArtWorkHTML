using System.Xml.Linq;

namespace ArtWorkHTML;

public partial class ArtworkHTML
{
  private sealed class SketchbookMetaData
  {
    public string IntroHtml = "";
    public Dictionary<int, (string DateRange, string CommentHtml)> Books = new();
  }

  // Reads sketchbooks.xml (shipped next to the exe) into editorial copy used by
  // the sketchbook pages: the landing-page <intro> and per-book date range +
  // <comment>. Missing/unparseable file -> empty result (pages fall back to the
  // hardcoded text). <encryptemail> is rendered with the JS obfuscation scheme.
  private SketchbookMetaData LoadSketchbookMeta()
  {
    var meta = new SketchbookMetaData();
    // sketchbooks.xml is source (not shipped to the output site); it lives in the
    // project directory, which is the working directory under `dotnet run`.
    var path = Path.Combine(Directory.GetCurrentDirectory(), "sketchbooks.xml");
    if (!File.Exists(path))
    {
      Console.WriteLine($"  ! sketchbooks.xml not found at {path} — using fallback sketchbook text");
      return meta;
    }

    try
    {
      var root = XDocument.Load(path).Root; // <sketch>
      var intro = root?.Element("intro");
      if (intro != null) meta.IntroHtml = RenderRichText(intro);

      var books = root?.Element("books");
      if (books != null)
      {
        foreach (var book in books.Elements("book"))
        {
          if (!int.TryParse((string?)book.Attribute("number"), out int num)) continue;
          var dates = book.Element("dates");
          string start = (dates?.Element("start")?.Value ?? "").Trim();
          string end   = (dates?.Element("end")?.Value ?? "").Trim();
          string range = (start.Length, end.Length) switch
          {
            (0, 0) => "",
            (_, 0) => start,
            (0, _) => end,
            _      => $"{start} – {end}"
          };
          var comment = book.Element("comment");
          string commentHtml = comment != null ? RenderRichText(comment) : "";
          meta.Books[num] = (range, commentHtml);
        }
      }
    }
    catch (Exception ex)
    {
      Console.WriteLine($"  ! Failed to parse sketchbooks.xml: {ex.Message} — using fallback sketchbook text");
    }

    return meta;
  }

  // <intro>/<comment> -> HTML. <p> children become <p> blocks; plain-text
  // content is wrapped in a single <p>.
  private string RenderRichText(XElement el)
  {
    var sb = new System.Text.StringBuilder();
    var paras = el.Elements("p").ToList();
    if (paras.Count > 0)
    {
      foreach (var p in paras)
        sb.Append("<p>").Append(RenderInline(p)).Append("</p>");
    }
    else
    {
      var inline = RenderInline(el).Trim();
      if (inline.Length > 0) sb.Append("<p>").Append(inline).Append("</p>");
    }
    return sb.ToString();
  }

  // Renders inline content: text is HTML-escaped; <encryptemail> becomes an
  // obfuscated mailto link; any other wrapper element renders its contents.
  private string RenderInline(XElement el)
  {
    var sb = new System.Text.StringBuilder();
    foreach (var node in el.Nodes())
    {
      if (node is XText t)
        sb.Append(EscapeHtml(t.Value));
      else if (node is XElement e)
      {
        if (string.Equals(e.Name.LocalName, "encryptemail", StringComparison.OrdinalIgnoreCase))
          sb.Append(ObfuscatedEmailLink(e.Value.Trim()));
        else if (string.Equals(e.Name.LocalName, "p", StringComparison.OrdinalIgnoreCase))
          sb.Append("<p>").Append(RenderInline(e)).Append("</p>");
        else
          sb.Append(RenderInline(e));
      }
    }
    return sb.ToString();
  }
}
