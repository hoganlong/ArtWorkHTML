using System.Text;

namespace ArtWorkHTML;

public partial class ArtworkHTML
{
  private async Task GenerateOpensourcePage()
  {
    var html = new StringBuilder();
    html.AppendLine(GetHtmlHeader("Open Source — Keith Long Archive"));
    html.AppendLine(@"
    <div class='landing-header'>
      <h1>Keith Long Archive</h1>
      <p class='subtitle'>Open Source</p>
    </div>
    <div class='landing-header'>
        <p class='subtitle'><a href='index.html'>← Back to Archive</a></p>
        <div class='landing-content'>
            <p>The source code for this website will be made open source eventually.  I haven't done it all yet.  But I did one program you can find the details below.</p>
            <p>If you want access to the others before I'm ready just let me know and we will work it out.</p>
            <table class='std-table'>
            <tr><th>System</th><th>Code Location</th></tr>
            <tr><td>Generate Airtable Schema</td><td><a href='https://github.com/hoganlong/AirtableSchemaReader'>Github</a></td></tr>
            <tr><td>ETL Airtable->PostgreSQL</td><td>TBD</td></tr>
            <tr><td>Airtable image reader</td><td>TBD</td></tr>
            <tr><td>HTML Generator</td><td>TBD</td></tr>
            </table>
  
        </div>
    </div>");
    html.AppendLine(GetHtmlFooter());
    await File.WriteAllTextAsync(Path.Combine(_outputDirectory, "opensource.html"), html.ToString());
  }
}
