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
            <p>The source code for this website will be made open source eventually.  I have started on the documentation and you can 
            find that project (that describes how to create a website like this) here:
            <a href='https://github.com/hoganlong/ArchiveSystem'>Archive System on GitHub</a>
            You will see it references projects that are not public yet, but will be soon.  You can see details of all the projects needed below.
            Following me on GitHub or social media will be the easest way to find out about updates.
            </p>
            <table class='std-table'>
            <tr><th>System</th><th>Code Location</th></tr>
            <tr><td>Documentation of system</td><td><a href='https://github.com/hoganlong/ArchiveSystem'>Github</a></td></tr>
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
