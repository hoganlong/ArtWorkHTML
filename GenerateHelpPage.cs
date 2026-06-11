using System.Text;

namespace ArtWorkHTML;

public partial class ArtworkHTML
{
  private async Task GenerateHelpPage(List<int>? years = null, Dictionary<int, List<int>>? sketchPages = null)
  {
    var html = new StringBuilder();
    html.AppendLine(GetHtmlHeader("Help — Keith Long Archive"));
    html.AppendLine(@"
    <div class='landing-header'>
      <h1>Keith Long Archive</h1>
      <p class='subtitle'>Help</p>
    </div>
    <div class='landing-header'>
        <p class='subtitle'><a href='index.html'>← Back to Archive</a></p>
        <div class='landing-content'>
        <P>You can use the sections below to find specific images</P>
        </div>
    </div>");

    if (years != null && years.Count > 0)
    {
      var yearOptions = new StringBuilder();
      foreach (var year in years)
        yearOptions.AppendLine($"        <option value='{year}'>{year}</option>");

      var typeOptions = new StringBuilder();
      foreach (var kvp in TypeDescriptions)
        typeOptions.AppendLine($"        <option value='{kvp.Key}'>{kvp.Value}</option>");

      html.AppendLine($@"
    <div class='landing-header'>
      <h2>Find Artwork</h2>
      <div class='landing-content'>
        <div class='find-form'>
          <select id='find-year'>
            <option value=''>Year</option>
{yearOptions}          </select>
          <select id='find-type'>
            <option value=''>Type</option>
{typeOptions}          </select>
          <input type='number' id='find-num' min='1' max='9999' placeholder='Number' />
          <button onclick='findArtwork()'>View</button>
        </div>
      </div>
    </div>
    <script>
    function findArtwork() {{
      var year = document.getElementById('find-year').value;
      var type = document.getElementById('find-type').value;
      var num = document.getElementById('find-num').value;
      if (!year || !type || !num) {{ alert('Please select year, type, and enter a number.'); return; }}
      var padded = String(parseInt(num)).padStart(4, '0');
      var id = 'KL_' + year + '_' + type + '_' + padded;
      window.location.href = 'artwork.html?show=' + id + '&back=help.html&backlabel=Return+to+Help';
    }}
    </script>");
    }

    if (sketchPages != null && sketchPages.Count > 0)
    {
      // Build JS data object: { bookNum: [page, page, ...], ... }
      var jsData = new StringBuilder();
      jsData.Append("var sketchData = {");
      bool firstBook = true;
      foreach (var kvp in sketchPages.OrderBy(k => k.Key))
      {
        if (!firstBook) jsData.Append(",");
        firstBook = false;
        jsData.Append($"{kvp.Key}:[{string.Join(",", kvp.Value)}]");
      }
      jsData.Append("};");

      var bookOptions = new StringBuilder();
      foreach (var bookNum in sketchPages.Keys.OrderBy(k => k))
        bookOptions.AppendLine($"        <option value='{bookNum}'>Book {bookNum}</option>");

      html.AppendLine($@"<BR>
    <div class='landing-header'>
      <h2>Find Sketch</h2>
      <div class='landing-content'>
        <div class='find-form'>
          <select id='sketch-book' onchange='sketchBookChanged()'>
            <option value=''>Book</option>
{bookOptions}          </select>
          <select id='sketch-page'>
            <option value=''>Page</option>
          </select>
          <button onclick='findSketch()'>View</button>
        </div>
      </div>
    </div>
    <script>
    {jsData}
    function sketchBookChanged() {{
      var book = document.getElementById('sketch-book').value;
      var pageSelect = document.getElementById('sketch-page');
      pageSelect.innerHTML = '<option value="""">Page</option>';
      if (!book || !sketchData[book]) return;
      sketchData[book].forEach(function(p) {{
        var opt = document.createElement('option');
        opt.value = p;
        opt.textContent = 'Page ' + p;
        pageSelect.appendChild(opt);
      }});
    }}
    function findSketch() {{
      var book = document.getElementById('sketch-book').value;
      var page = document.getElementById('sketch-page').value;
      if (!book || !page) {{ alert('Please select a book and page.'); return; }}
      var tag = 'B' + book + '_P' + page;
      window.location.href = 'sketchbooks/sketchbook' + book + '.html?show=' + tag + '&back=../help.html&backlabel=Return+to+Help';
    }}
    </script>");
    }

    html.AppendLine(GetHtmlFooter());
    await File.WriteAllTextAsync(Path.Combine(_outputDirectory, "help.html"), html.ToString());
  }
}
