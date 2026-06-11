using System.Text;

namespace ArtWorkHTML;

public partial class ArtworkHTML
{
  private async Task GenerateHowIsMadePage()
  {
    var html = new StringBuilder();
    html.AppendLine(GetHtmlHeader("How It Is Made — Keith Long Archive"));
    html.AppendLine(@"
    <div class='landing-header'>
      <h1>Keith Long Archive</h1>
      <p class='subtitle'>How It Is Made</p>
    </div>
    <div class='landing-header'>
      <p class='subtitle'><a href='index.html'>← Back to Archive</a></p>
      <div class='landing-content'>

      <h2>Technical Details</h2>
      <h3>Artwork Catalogue Creation</h3>
       <p>To enable the creation of the catalogue (a database of all artwork details), we used a web service/website called <i>Airtable</i>.  This allowed us to have multiple people on the team add to the database at the same time (mostly just myself and the archivist).  It also featured easy to use UI for creating and modifying tables allowing for an incremental DB design enabling a more agile approach as we discovered needs or problems. Airtable also had a nice feature which allowed us to upload images and have them be visible as thumbnails as we were working on the catalogue.  This feature made the project much easier as we had pictures of the artwork we could reference when needed.</p>

      <h3>Database Migration</h3>
      <p>From the start, I knew that writing a program to generate the HTML would require a database I had full control over — Airtable alone would not be sufficient.
      Airtable is a closed system: it provides a polished UI, but it does not allow direct database access or SQL queries.
      Being able to write and run arbitrary SQL was essential for developing and testing the more complex parts of the system.
      So I created a database migration system to move the data from Airtable to a PostgreSQL database.  The system is comprised of 3 programs:
      <ol type='1'>
      <li style='margin-left:1em'>A program that reads the schema of the Airtable files and creates a schema definition file.</li>
      <li style='margin-left:1em'>A program that reads the schema definition file and makes sure the tables as described exist on the SQL server and then reads the Airtable data and writes it to the SQL server.  It also keeps a log and only reads data that has changed since the last update, allowing for incremental data migration.</li>
      <li style='margin-left:1em'>A program that extracts all the images uploaded to Airtable and creates jpg files.  This program is also incremental and only extracts new files.</li>
      </ol></p>

      <h3>HTML Generation</h3>
      <p>I then wrote a C# console application that reads the PostgreSQL database and generates the complete set of static HTML files that make up this website — including the very page you are reading now.
      Each time it runs, it regenerates all pages from scratch, pulling the latest artwork data, image references, and metadata from the database.
      The result is a fully self-contained static website with no server-side code required to view it.
      The date and time the pages were last generated is recorded at the bottom of every page.</p>

      <h3>What about the pictures?</h3>
      <p>The pictures of the artworks were taken by a professional photographer and color processed by him then uploaded to web cloud storage 
      using file names stored in the DB.  (This allowed the HTML generator to reference them in the page generation.)   
      In a similar way the scans were performed by professional scanners and stored on the web cloud storage.  The scanners provided a metadata record for each scan containing: the filename, sketchbook number, page number, date (when readable via OCR), and a notes field — used so far to flag pages that were attachments taped into the book rather than bound pages. This metadata was uploaded to the database and is what allows the HTML generator to correctly organize and display the sketchbook pages.</p>
      <p>In addition to the sketchbook scans, we also have a collection of scans of various documents related to Keith's life and work.  
      These include: slides, photos, drawings, letters, notes, clippings, and other miscellaneous items.</p> 
      <p>Our internal team also scanned some of the smaller items using a high-resolution personal tabletop scanner.  We created software to convert the tif files from the scanner to jpg format.</p>

      <h3>Deployments and upload</h3> 
      <p>The final step of the process is to upload the generated HTML files and images to cloud storage and make them available on the web.  We used AWS S3 for storage and CloudFront for content delivery.  The upload process is automated using a script that syncs the generated local files with the S3 bucket and then clears the CloudFront cache.</p> 

      <h3>Additional details and tools</h3>
      <table class='std-table'>
        <tr><td>Cloud Storage (images)...</td><td>AWS S3</td></tr>
        <tr><td>Cloud Storage (website)...</td><td>AWS S3->Cloud Front</td></tr>
        <tr><td>SQL DB Hosting...</td><td>Amazon Aurora/RDS</td></tr>
        <tr><td>Version Control...</td><td>GitHub</td></tr>
        <tr><td>Code generation ""AI""...</td><td>Claude Code (~v4.6)</td></tr>
        <tr><td>Languages used...</td><td>C#, HTML, JavaScript, <small>(unknown AirTable script)</small></td></tr>
      </table>

      <h3>Take a look at the code</h3>
      <p>All the code for the database migration and HTML generation is available on GitHub: <a href='https://github.com/hoganlong/ArchiveSystem'>Hogan's Archive System</a> This project includes all the technical documentation and source code for the project.  Nothing is hidden or proprietary.</p>




      </div>
    </div>");
    html.AppendLine(GetHtmlFooter());
    await File.WriteAllTextAsync(Path.Combine(_outputDirectory, "howisitmade.html"), html.ToString());
  }
}
