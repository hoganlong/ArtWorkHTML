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
    await GenerateSeriesPages();
    await GenerateLocationPages();
    await GenerateStylesheet();

    Console.WriteLine("  ✓ index.html - Landing page");
    Console.WriteLine("  ✓ statistics.html - Archive statistics");
    Console.WriteLine("  ✓ artworksplus.html - Complete artwork list");
    Console.WriteLine("  ✓ series.html - Artworks by series");
    Console.WriteLine("  ✓ locations.html - Artworks by location");
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
            <a href='series.html' class='nav-button'>📚 View by Series</a>
            <a href='locations.html' class='nav-button'>📍 View by Location</a>
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

/*
  private async Task GenerateArtworkListPages()
  {
    // URL templates for S3 images
    const string S3_ARTWORK_IMAGE_URL = "https://keithlong-art-photos.s3.us-east-1.amazonaws.com/atch/artwork_{0}_{1}.jpg";

    ArtList artList = new();
    ArtList polaroidList = new();
    ArtList sketchBookList = new();
 //   Dictionary<int, ArtList> sketchBookLists = [];

    // Get all artworks from the database
    await using var connection = new NpgsqlConnection(_connectionString);
    await connection.OpenAsync();

    // Do the Artwork SQL
    var sql = @"

        -- we should add a check there are no direct images that are not front and back view
        -- and there are no attachments that are not front, back, paper or palaroid 
        with img as 
        (
          select ai.id_field, ai.artwork_id, LEFT(ai.view , 4) as lview 
          from artwork_image ai
          where 
            (ai.view LIKE 'Back%' OR
             ai.view LIKE 'Front%' OR
             ai.view LIKE 'Paper%' OR
             ai.view LIKE 'Polaroid%')
            and URL is null 
        ), imgGroup as 
        (
          select array_to_string(array_agg(id_field), ', ') as ids, artwork_id, lview
          from img 
          group by artwork_id, img.lview 
        ), dirimg as 
        (
          select ai.url, ai.artwork_id, LEFT(ai.view , 4) as lview 
          from artwork_image ai
          where ai.URL is not null 
        ), dirimgGroup as 
        (
          select array_to_string(array_agg(url), ', ') as dirimgs, artwork_id, lview
          from dirimg 
          group by artwork_id, dirimg.lview 
        )
        SELECT
          a.id_field, a.FileName, a.title, a.series, a.create_dt, a.medium, a.dimensions, a.FOLDED_DIMENSIONS,
          a.location, a.notes, a.human_readable_id, a.artwork_image_id,
          ai_back.ids as back_id,   -- 12
          ai_front.ids as front_id, -- 13
          ai_paper.ids as paper_id, -- 14
          ai_polaroid.ids as polaroid_id, -- 15
          diri_back.dirimgs as back_imgs, -- 16  
          diri_front.dirimgs as front_imgs, -- 17
          t.code, t.description as type_desc -- 18, 19
        FROM artwork a
        LEFT JOIN artwork_type t ON a.type_id ->> 0 = t.airtable_id
        LEFT JOIN imgGroup ai_back ON a.airtable_id = ai_back.artwork_id AND ai_back.lview    ='Back'
        LEFT JOIN imgGroup ai_front ON a.airtable_id = ai_front.artwork_id AND ai_front.lview ='Fron'
        LEFT JOIN imgGroup ai_paper ON a.airtable_id = ai_paper.artwork_id AND ai_paper.lview ='Pape'
        LEFT JOIN imgGroup ai_polaroid ON a.airtable_id = ai_polaroid.artwork_id AND ai_polaroid.lview ='Pola'
        LEFT JOIN dirimgGroup diri_back ON a.airtable_id = diri_back.artwork_id AND diri_back.lview    ='Back'
        LEFT JOIN dirimgGroup diri_front ON a.airtable_id = diri_front.artwork_id AND diri_front.lview ='Fron'
        where ai_back.ids is not null or ai_front.ids is not null or ai_paper.ids is not null or ai_polaroid.ids is not null or diri_back.dirimgs is not null or diri_front.dirimgs is not null
        ORDER BY a.human_readable_id, a.create_dt ASC NULLS last";

    await using var cmd = new NpgsqlCommand(sql, connection);
    await using var reader = await cmd.ExecuteReaderAsync();

    while (await reader.ReadAsync())
    {
      var backId = reader.IsDBNull(12) ? null : reader.GetString(12).Split(',').
              Select(s => int.TryParse(s.Trim(), out int id) ? (int?)id : null).Where(id => id.HasValue).Select(id => id!.Value).ToArray();
      var frontId = reader.IsDBNull(13) ? null : reader.GetString(13).Split(',').
              Select(s => int.TryParse(s.Trim(), out int id) ? (int?)id : null).Where(id => id.HasValue).Select(id => id!.Value).ToArray();
      var paperId = reader.IsDBNull(14) ? null : reader.GetString(14).Split(',').
              Select(s => int.TryParse(s.Trim(), out int id) ? (int?)id : null).Where(id => id.HasValue).Select(id => id!.Value).ToArray();
      var polaroidId = reader.IsDBNull(15) ? null : reader.GetString(15).Split(',').
              Select(s => int.TryParse(s.Trim(), out int id) ? (int?)id : null).Where(id => id.HasValue).Select(id => id!.Value).ToArray();

      var backFileName = reader.IsDBNull(16) ? null : reader.GetString(16).Split(',').Select(s => s.Trim()).ToArray();
      var frontFileName = reader.IsDBNull(17) ? null : reader.GetString(17).Split(',').Select(s => s.Trim()).ToArray();


      Artwork artwork = new(reader.GetInt32(0).ToString(), reader.IsDBNull(1) ? "" : reader.GetString(1),
         reader.IsDBNull(2) ? "" : reader.GetString(2), reader.IsDBNull(3) ? "" : reader.GetString(3),
          reader.IsDBNull(4) ? DateTime.MinValue : reader.GetDateTime(4), reader.IsDBNull(5) ? "" : reader.GetString(5),
          reader.IsDBNull(6) ? "" : reader.GetString(6), reader.IsDBNull(7) ? "" : reader.GetString(7),
          reader.IsDBNull(8) ? "" : reader.GetString(8), reader.IsDBNull(9) ? "" : reader.GetString(9),
          reader.IsDBNull(10) ? "" : reader.GetString(10), reader.IsDBNull(11) ? "" : reader.GetString(11),
          reader.IsDBNull(18) ? "" : reader.GetString(18),
          backId, frontId, paperId, polaroidId,backFileName, frontFileName);  

      artList.AddArtwork(artwork);
    } // while reader.ReadAsync()
    reader.Close();
    cmd.Dispose();

    // Do the Sketchbook SQL
    sql = @"
       select s.airtable_id, s.sketch_dt, s.description, s.sketch_loc, s.sketch_people,
              s.sketch_medium, s.sketchbook_number, s.page_number, s.artwork_id, s.filename, s.pub_notes
       from sketch s 
       order by s.sketchbook_number asc, s.page_number asc";

    await using var sketchcmd = new NpgsqlCommand(sql, connection);
    await using var sketchreader = await sketchcmd.ExecuteReaderAsync();

    while (await sketchreader.ReadAsync())
    {
      string airtable_id= reader.IsDBNull(0) ? "" : reader.GetString(0);
      DateTime ctDate = reader.IsDBNull(1) ? DateTime.MinValue : reader.GetDateTime(1);
      string description = reader.IsDBNull(2) ? "" : reader.GetString(2);
      string location = reader.IsDBNull(3) ? "" : reader.GetString(3);
      string people = reader.IsDBNull(4) ? "" : reader.GetString(4);
      string medium = reader.IsDBNull(5) ? "" : reader.GetString(5);
      int sketchbookNumber = reader.IsDBNull(6) ? 0 : reader.GetInt32(6);
      int pageNumber = reader.IsDBNull(7) ? 0 : reader.GetInt32(7);
      string? artworkID = reader.IsDBNull(8) ? null : reader.GetString(8);
      string filename = reader.IsDBNull(9) ? "" : reader.GetString(9);
      string pubNotes = reader.IsDBNull(10) ? "" : reader.GetString(10);

      Artwork sketch = new(airtable_id, ctDate, location, people, medium, sketchbookNumber, pageNumber, artworkID, pubNotes, filename);
      sketchBookList.AddArtwork(sketch);  
    } // while reader.ReadAsync()
    sketchreader.Close();
    sketchcmd.Dispose();  

    // Now get the bucket data
    string bucketName = "keithlong-art-photos";
    string region = "us-east-1";

    try
    {
      var s3Client = new AmazonS3Client(Amazon.RegionEndpoint.GetBySystemName(region));

      Console.WriteLine($"Connecting to S3 bucket: {bucketName} in region: {region}");
      Console.WriteLine("Listing files...\n");

      var request = new ListObjectsV2Request
      {
        BucketName = bucketName
      };

      ListObjectsV2Response response;
      int totalBucketFiles = 0;

      //update
      int skippedBucketFiles = 0;
      int tifBucketFiles = 0;
      int scanBucketFiles = 0;
      int scanJPGBucketFiles = 0;
      int JPGBucketFiles = 0;
      int atchBucketFiles = 0;

      //      string title = $"Keith Long Archive Polaroid Image List (generated - {DateTime.Now.ToLongDateString()} at {DateTime.Now.ToLongTimeString()}";
      /*
            StringBuilder htmlContent = new StringBuilder();
            htmlContent.Append("<!DOCTYPE html>\n");
            htmlContent.Append("<html>\n");
            htmlContent.AppendLine("<title>"+title+"</title>");

            htmlContent.Append("<style>\n");
            htmlContent.Append("  div.gallery \n");
            htmlContent.Append("  {\n");
            htmlContent.Append("    display: flex;\n");
            htmlContent.Append("    flex-wrap: wrap;\n");
            htmlContent.Append("    justify-content: flex-start;\n");
            htmlContent.Append("  }\n");

            htmlContent.Append("  div.gallery-item\n");
            htmlContent.Append("  {\n");
            htmlContent.Append("    margin: 5px;\n");
            htmlContent.Append("    border: 1px solid #ccc;\n");
            htmlContent.Append("    width: 300px;\n");
            htmlContent.Append("  }\n");

            htmlContent.Append("  div.gallery-item:hover\n");
            htmlContent.Append("  {\n");
            htmlContent.Append("    border: 1px solid #777;\n");
            htmlContent.Append("  }\n");

            htmlContent.Append("  div.gallery-item img\n");
            htmlContent.Append("  {\n");
            htmlContent.Append("    width: 100%;\n");
            htmlContent.Append("    height: auto;\n");
            htmlContent.Append("  }\n");

            htmlContent.Append("  div.gallery-item div.desc\n");
            htmlContent.Append("  {\n");
            htmlContent.Append("    padding: 5px;\n");
            htmlContent.Append("    text-align: center;\n");
            htmlContent.Append("  }\n");
            htmlContent.Append("</style>\n");

            htmlContent.Append("</style>\n");
            htmlContent.Append("</head>\n");

            htmlContent.Append("<body>\n");
            htmlContent.AppendLine("<h1>"+title+"</h1><br/>");
            htmlContent.Append("<div class= \"gallery\" >\n");
      */
      /*
      do
      {
        response = await s3Client.ListObjectsV2Async(request);

        foreach (Amazon.S3.Model.S3Object obj in response.S3Objects)
        {
          totalBucketFiles++;
          string fullPath = obj.Key;

          int slashPos = fullPath.LastIndexOf('/');
          string dir = slashPos > 0 ? fullPath[0..slashPos]:"";
          string filename = fullPath[(slashPos + 1)..];
          int dotLoc = filename.LastIndexOf('.');
          string name = (dotLoc == -1) ? filename : filename[0..dotLoc];
          string ext = (dotLoc == -1) ? "" : filename[(dotLoc + 1)..].ToLower();
          DateTime? lastModified = obj.LastModified;

          if (dir == "scans" && ext == "tif" && filename.StartsWith("KLA")) // It's a sketchbook TIF so add it to the sketchbook list
          {
            sketchBookList.AddBucketFile("scans/", name, ext, lastModified, true);  // add that puppy.
            continue;
          }

          if (fullPath.Length > 10 && fullPath[0..9] == "scans/jpg")
          {
            // not doing anything with scans right now, but want to keep track of how many there are in the bucket
            scanJPGBucketFiles++;
            if (fullPath.EndsWith('/'))
            {
              continue;
            }
            if (ext == ".pdf")
            {
              // should have error msg for pdf files in scans/jpg dir since we aren't expecting them, but for now just skip them and keep track of how many there are 
              skippedBucketFiles++;
              continue;
            }
            if (dir == "scans/jpg" && filename.StartsWith("KLA")) // It's a sketchbook image so add it to the sketchbook list
            {
              sketchBookList.AddBucketFile("scans/", name, ext, lastModified, true);  // add that puppy.
              continue;
            }
            else  // It's a polaroid image so add it to the polaroid list
            {
            //  polaroidList.AddBucketFile("scans/", fullPath[10..], "jpg", obj.LastModified);
              polaroidList.AddBucketFile("scans/", name, ext, lastModified,true);
              continue;
            }
          }
          else
          {
            // Just the dir ignore it.
            if (fullPath.EndsWith("/"))
            {
              continue;
            }
            if (slashPos == -1)
            {
              if (ext == "tif")
              {
                // should check if in correct (expected) location in bucket, but for now just set the state
                artList.AddBucketFile(dir, name, ext, lastModified);
                JPGBucketFiles++;
              }
      //        JPGBucketFiles++;
              continue;
            }
            else
            {

//              Console.WriteLine($"dir: {dir}, filename: {filename}, name: {name}, ext: {ext}");

              if (ext == "pdf")
              {
                skippedBucketFiles++;
                continue;
              }

              switch (dir)
              {
                case "jpg":
                  if (ext == "jpg")
                  {
                    // should really check if in correct (expected) location in bucket, but for now just set the state
                    artList.AddBucketFile(dir, name, ext, lastModified);
                    JPGBucketFiles++;
                  }
                  else
                  {
                    Console.WriteLine($"Expected jpg extension but found: {ext} in file: {fullPath}");
                    skippedBucketFiles++;
                    continue;
                  }
                  break;
                case "scans":
                  scanBucketFiles++;
                  break;
                case "scans/jpg":
                  scanJPGBucketFiles++;
                  break;
                case "atch":
                  atchBucketFiles++;
                  // should really check if in correct (expected) location in bucket, but for now just set the state
                  break;
                default:
                  Console.WriteLine($"Unknown directory: {dir} in file: {fullPath}");
                  break;
              }
            }
          }
        }
        request.ContinuationToken = response.NextContinuationToken;
      } while (response.IsTruncated == true);

      Console.WriteLine($"Total files in bucket: {totalBucketFiles}");
      Console.WriteLine($"Total JPG files in bucket: {JPGBucketFiles}");
      Console.WriteLine($"Total scan JPG files in bucket: {scanJPGBucketFiles}");
      Console.WriteLine($"Total scan files in bucket: {scanBucketFiles}");
      Console.WriteLine($"Total tif files in bucket: {tifBucketFiles}");
      Console.WriteLine($"Total attachment files in bucket: {atchBucketFiles}");
    }

    catch (AmazonS3Exception ex)
    {
      Console.WriteLine($"AWS S3 Error: {ex.Message}");
      Console.WriteLine($"Error Code: {ex.ErrorCode}");
    }
    catch (Exception ex)
    {
      Console.WriteLine($"Error: {ex.Message}");
    }

    // Now generate the HTML page using the artList
    var html = new StringBuilder();
    #region all artwork list page
    html.AppendLine(GetHtmlHeader("All Artworks - Keith Long Archive"));

    html.AppendLine(@"
    <div class='container'>
        <h1>All Artworks</h1>
        <p class='subtitle'><a href='index.html'>← Back to Home</a></p>
    </div>
    <div class='page-controls'>
        <div class='page-controls-row'>
            <span class='page-controls-label'>Hover effects:</span>
            <label><input type='checkbox' id='chk-thumb-hover' checked onchange='document.body.classList.toggle(""no-thumb-hover"", !this.checked)'> Thumbnail preview (p)</label>
            <label><input type='checkbox' id='chk-image-hover' checked onchange='document.body.classList.toggle(""no-image-hover"", !this.checked)'> Image zoom (z)</label>
        </div>
        <div class='page-controls-row'>
            <span class='page-controls-label'>Show types:</span>
            <span id='type-filter-checkboxes'></span>
        </div>
    </div>
    <script>
    document.addEventListener('DOMContentLoaded', function() {
        var items = document.querySelectorAll('.gallery-item');
        var types = {};
        items.forEach(function(el) {
            var code = el.getAttribute('data-typecode') || '(none)';
            var desc = el.getAttribute('data-typedesc') || code;
            if (!types[code]) types[code] = desc;
        });
        var container = document.getElementById('type-filter-checkboxes');
        if (!container) return;

        // All master checkbox — controls all type boxes but is not affected by them
        var allLabel = document.createElement('label');
        var allCb = document.createElement('input');
        allCb.type = 'checkbox';
        allCb.checked = true;
        allCb.addEventListener('change', function() {
            container.querySelectorAll('input[data-filter-type]').forEach(function(cb) {
                cb.checked = allCb.checked;
            });
            filterGallery();
        });
        allLabel.appendChild(allCb);
        allLabel.appendChild(document.createTextNode(' All'));
        container.appendChild(allLabel);

        Object.keys(types).sort().forEach(function(code) {
            var label = document.createElement('label');
            var cb = document.createElement('input');
            cb.type = 'checkbox';
            cb.checked = true;
            cb.dataset.filterType = code;
            cb.addEventListener('change', filterGallery);
            label.appendChild(cb);
            label.appendChild(document.createTextNode(' ' + types[code]));
            container.appendChild(label);
        });
        function filterGallery() {
            var hidden = new Set();
            container.querySelectorAll('input[data-filter-type]').forEach(function(cb) {
                if (!cb.checked) hidden.add(cb.dataset.filterType);
            });
            items.forEach(function(el) {
                var t = el.getAttribute('data-typecode') || '(none)';
                el.style.display = hidden.has(t) ? 'none' : '';
            });
        }
    });
    </script>
    <script>
    document.addEventListener('keydown', function(e) {
        if (e.target.tagName === 'INPUT' || e.target.tagName === 'TEXTAREA') return;
        if (e.key === 'z' || e.key === 'Z') document.getElementById('chk-image-hover')?.click();
        if (e.key === 'p' || e.key === 'P') document.getElementById('chk-thumb-hover')?.click();
        if (e.key === 't' || e.key === 'T') window.scrollTo({ top: 0, behavior: 'smooth' });
    });
    </script>
    <div class='container'>");

    html.AppendLine("<div class='gallery' style='font-size: x-small;'>");
    foreach (var artItem in artList.artworks) // while (await reader.ReadAsync())
    {
      Artwork art = artItem.Value;
/*
      if (art.states.HasFlag(StatesType.jpgFound))
      {
        Console.WriteLine("Found jpg for artwork: " + art.humanId);
      }


*/
/*
      html.AppendLine($@"<div class='gallery-item' data-typecode='{EscapeHtml(art.typeCode)}' data-typedesc='{EscapeHtml(GetTypeDescription(art.typeCode))}'>");
      if ((art.states & StatesType.NoImage) == 0) // if we have an image
      {
        html.AppendLine($@"  <a href='{art.jpgURL}' target='_blank' rel='noopener noreferrer'>
                   <img src='{art.jpgURL}' title='(click for full size)'/>
                    </a><br/>
                    <div class='desc'><a class='desc' href='{art.tifURL}'>[tif file]</a></div>");
      }
        // Add thumbnail buttons for additional views
        var thumbnails = new List<(string label, int[]? id)>
        {
          ("Front", art.frontId),
          ("Back", art.backId),
          ("Paper", art.paperId),
          ("Polaroid", art.polaroidId)
        };

        var filethumbnails = new List<(string label, string[]? names)>
        {
          ("Front", art.frontFileName),
          ("Back", art.backFileName)
        };

        List<string> thumbButtons = [];

        bool hasMult = false;

        foreach (var thumb in thumbnails)
        {
          if (thumb.id is not null && thumb.id.Length > 0)
          {
            hasMult = thumb.id.Length > 1;
            int curNum = 1;
            // We have at least one thumbnail for this view, so we can break out of the loop and generate buttons
            foreach (var id in thumb.id)
            {
              if (id != 0)
              {
                var thumbUrl = string.Format(S3_ARTWORK_IMAGE_URL, id, "small");
                var fullUrl = string.Format(S3_ARTWORK_IMAGE_URL, id, "full");
                var largeUrl = string.Format(S3_ARTWORK_IMAGE_URL, id, "large");
                thumbButtons.Add($"<a href='{fullUrl}' target='_blank' rel='noopener noreferrer' class='thumb-button' title='{thumb.label}{(hasMult?" "+curNum.ToString(): "")}'><img src='{thumbUrl}' width='36' height='36' /><img src='{largeUrl}' class='thumb-preview' /></a>");
              }
              curNum++;
            }
          }
        }

        foreach (var filethumb in filethumbnails)
        {
          if (filethumb.names is not null && filethumb.names.Length > 0)
          {
            hasMult = hasMult || filethumb.names.Length > 1;

            int curNum = 1;
            // We have at least one thumbnail for this view, so we can break out of the loop and generate buttons
            foreach (var name in filethumb.names)
            {
              if (!string.IsNullOrEmpty(name))
              {
                var url = art.MakeJPGURL(name);
                
                thumbButtons.Add($"<a href='{url}' target='_blank' rel='noopener noreferrer' class='thumb-button' title='{filethumb.label}(L){(hasMult?" "+curNum.ToString(): "")}'><img src='{url}' width='36' height='36' /><img src='{url}' width='100' height='100' class='thumb-preview' /></a>");
              }
              curNum++;
            }
          }
        }


        if (thumbButtons.Count != 0)
        {
          html.AppendLine($"  <div class='thumb-buttons'>");
          html.AppendLine($"    {string.Join(" ", thumbButtons)}");
          html.AppendLine($"  </div>");
        }
     
      html.AppendLine($"  <div class='desc'>");
      html.AppendLine($"    {BlankOrWithBR(art.title, "  ")}");
      html.AppendLine($"    {BlankOrWithBR(art.ctDate.ToShortDateString(), "  ")}");
      html.AppendLine($"    {BlankOrWithBR(art.medium, "  ")}");
      html.AppendLine($"    {BlankOrWithBR(art.dimensions, "  ")}");
      html.AppendLine($"    {BlankOrWithBR(art.foldedDimensions, "   Folded: ")}");
      html.AppendLine($"    {BlankOrWithBR(art.notes, "  Notes: ")}");
      html.AppendLine($"    {BlankOrWithBR(art.humanId, "  ")}");
      html.AppendLine($"    {BlankOrWithBR(GetTypeDescription(art.typeCode), "  Type: ")}");
      html.AppendLine($"  </div>");

      html.AppendLine($"<div class='desc' style='color:red;'>{String.Join("<br/>", art.errors)}</div>");

      if (art.states.HasFlag(StatesType.noDB))
        html.AppendLine($"<div class='desc'>Bucket name: {art.fileName}<br/></div>");

      if (art.states.HasFlag(StatesType.jpgFound))
        html.AppendLine($"<span class='heavy-check-mark'>&#x2705;</span>");
      if (art.states.HasFlag(StatesType.tifFound))
        html.AppendLine($"<span class='heavy-check-mark'>&#x2705;</span>");

      html.AppendLine($"</div>  <!-- gallery item -->");
    }

    html.AppendLine(@"</div>");
    html.AppendLine(GetHtmlFooter());

    await File.WriteAllTextAsync(Path.Combine(_outputDirectory, "artworksplus.html"), html.ToString());

    #endregion

    #region polaroid list page
    html.Clear();

    // Now generate the HTML page for the polaroid list
    html.AppendLine(GetHtmlHeader("{Polaroids - Keith Long Archive"));

    html.AppendLine(@"
    <div class='container'>
        <h1>Polaroids</h1>
        <p class='subtitle'><a href='index.html'>← Back to Home</a></p>
    </div>
    <div class='page-controls'>
        <span class='page-controls-label'>Hover effects:</span>
        <label><input type='checkbox' id='chk-image-hover' checked onchange='document.body.classList.toggle(""no-image-hover"", !this.checked)'> Image zoom (z)</label>
    </div>
    <script>
    document.addEventListener('keydown', function(e) {
        if (e.target.tagName === 'INPUT' || e.target.tagName === 'TEXTAREA') return;
        if (e.key === 'z' || e.key === 'Z') document.getElementById('chk-image-hover')?.click();
        if (e.key === 'p' || e.key === 'P') document.getElementById('chk-thumb-hover')?.click();
        if (e.key === 't' || e.key === 'T') window.scrollTo({ top: 0, behavior: 'smooth' });
    });
    </script>
    <div class='container'>");

    html.AppendLine("<div class='gallery' style='font-size: x-small;'>");
    foreach (var artItem in polaroidList.artworks) // while (await reader.ReadAsync())
    {
      Artwork art = artItem.Value;

      html.AppendLine($@"<div class='gallery-item'>");
      html.AppendLine($@"  <a href='{art.jpgURL}' target='_blank' rel='noopener noreferrer'><img src='{art.jpgURL}' title='(click for full size)'/></a><br/>
        <div class='desc'><a class='desc' href='{art.tifURL}'>[tif file]</a></div>");

      html.AppendLine($@"<div class='gallery-item'>");
      html.AppendLine($"  <div class='desc'>");
      html.AppendLine($"    {BlankOrWithBR(art.title, "  ")}");
      html.AppendLine($"    {BlankOrWithBR(art.ctDate.ToShortDateString(), "  ")}");
      html.AppendLine($"    {BlankOrWithBR(art.medium, "  ")}");
      html.AppendLine($"    {BlankOrWithBR(art.dimensions, "  ")}");
      html.AppendLine($"    {BlankOrWithBR(art.foldedDimensions, "   Folded: ")}");
      html.AppendLine($"    {BlankOrWithBR(art.notes, "  Notes: ")}");
      html.AppendLine($"    {BlankOrWithBR(art.humanId, "  ")}");
      html.AppendLine($"  </div>");

      html.AppendLine($"<div class='desc' style='color:red;'>{String.Join("<br/>", art.errors)}</div>");

      if (art.states.HasFlag(StatesType.noDB))
        html.AppendLine($"<div class='desc'>Bucket name: {art.fileName}<br/></div>");

      if (art.states.HasFlag(StatesType.jpgFound))
        html.AppendLine($"<span class='heavy-check-mark'>&#x2705;</span>");
      if (art.states.HasFlag(StatesType.tifFound))
        html.AppendLine($"<span class='heavy-check-mark'>&#x2705;</span>");

      html.AppendLine($"</div></div>  <!-- gallery item -->");
    }

    html.AppendLine(@"</div>");
    html.AppendLine(GetHtmlFooter());

    await File.WriteAllTextAsync(Path.Combine(_outputDirectory, "polaroids.html"), html.ToString());
    #endregion

    #region sketchbook list pages
    var lastSketchbookNumber = -1; 
    List<int> sketchbookNumbers = sketchBookList.artworks.Values.Where(e => e.myType.HasFlag(ArtType.Sketch)).Select(a => a.sketchbookNumber).Distinct().OrderBy(n => n).ToList();

     // Now generate the HTML page for each sketchbook list
    foreach (var sketchBookEntry in sketchBookList.artworks.Where(e => e.Value.myType.HasFlag(ArtType.Sketch)).OrderBy(e => e.Value.sketchbookNumber).ThenBy(e => e.Value.pageNumber)) 
    {
      int bookNumber = sketchBookEntry.Value.sketchbookNumber;

      if (bookNumber != lastSketchbookNumber)  // We've hit a new sketchbook, so we need to start a new HTML page (but first write out the previous one if this isn't the first sketchbook)
      {
        if (lastSketchbookNumber != -1) 
        {
          // If this is not the first sketchbook, we need to write out the previous one before starting a new one
          html.AppendLine(@"</div>"); // close container
          html.AppendLine(GetHtmlFooter());
          await File.WriteAllTextAsync(Path.Combine(_outputDirectory, $"sketchbook{lastSketchbookNumber}.html"), html.ToString());
        }
        lastSketchbookNumber = bookNumber;
        html.Clear();

        // Now generate the HTML page for the sketchbook list
        html.AppendLine(GetHtmlHeader($"Sketchbook {bookNumber} - Keith Long Archive"));

        html.AppendLine(@"
        <div class='container'>
          <h1>Sketchbooks</h1>
          <p class='subtitle'><a href='index.html'>← Back to Home</a></p>");

        // Add navigation for other sketchbooks if there are multiple
        if (sketchbookNumbers.Count > 1)
        {
          html.AppendLine("<div class='sketchbook-nav'><span class='sketchbook-nav-label'>Sketchbook:</span>");
          foreach (var entry in sketchbookNumbers)
          {
            if (entry == bookNumber)
              html.AppendLine($"<span class='nav-button sketchbook-nav-button active'>{entry}</span>");
            else
              html.AppendLine($"<a href='sketchbook{entry}.html' class='nav-button sketchbook-nav-button'>{entry}</a>");
          }
          html.AppendLine("</div>");
        }

        html.AppendLine(@"
        </div>
        <div class='page-controls'>
            <span class='page-controls-label'>Hover effects:</span>
            <label><input type='checkbox' id='chk-image-hover' checked onchange='document.body.classList.toggle(""no-image-hover"", !this.checked)'> Image zoom (z)</label>
        </div>
        <script>
        document.addEventListener('keydown', function(e) {
            if (e.target.tagName === 'INPUT' || e.target.tagName === 'TEXTAREA') return;
            if (e.key === 'z' || e.key === 'Z') document.getElementById('chk-image-hover')?.click();
            if (e.key === 'p' || e.key === 'P') document.getElementById('chk-thumb-hover')?.click();
            if (e.key === 't' || e.key === 'T') window.scrollTo({ top: 0, behavior: 'smooth' });
        });
        </script>
        <div class='container'>");

        html.AppendLine("<div class='gallery' style='font-size: x-small;'>");
      }

      Artwork art = sketchBookEntry.Value;

      html.AppendLine($@"<div class='gallery-item'>");
      html.AppendLine($@"  <a href='{art.jpgURL}' target='_blank' rel='noopener noreferrer'><img src='{art.jpgURL}' title='(click for full size)'/></a><br/>
        <div class='desc'><a class='desc' href='{art.tifURL}'>[tif file]</a></div>");

      html.AppendLine($"  <div class='desc'>");
      html.AppendLine($"    {BlankOrWithBR(art.pageNumber.ToString(), " ")}");
      html.AppendLine($"    {BlankOrWithBR(art.ctDate.ToShortDateString(), "  ")}");

      html.AppendLine($"    {BlankOrWithBR(art.location, "  ")}");
      html.AppendLine($"    {BlankOrWithBR(art.medium, "  ")}");
      html.AppendLine($"    {BlankOrWithBR(art.people, "  ")}");
      html.AppendLine($"    {BlankOrWithBR(art.notes, "  Notes: ")}");

      // technical detail items
      html.AppendLine($"{BlankOrComment(art.id, "id: ")}");
      html.AppendLine($"{BlankOrComment(art.artworkID!, "artid: ")}");
      html.AppendLine($"{BlankOrComment(art.humanId, "humandId: ")}");
      html.AppendLine($"{BlankOrComment(art.fileName, "filename: ")}");

      html.AppendLine($"  </div>");

      html.AppendLine($"<div class='desc' style='color:red;'>{String.Join("<br/>", art.errors)}</div>");

      if (art.states.HasFlag(StatesType.jpgFound))
        html.AppendLine($"<span class='heavy-check-mark'>&#x2705;</span>");
      if (art.states.HasFlag(StatesType.tifFound))
        html.AppendLine($"<span class='heavy-check-mark'>&#x2705;</span>");

      html.AppendLine($"</div>  <!-- gallery item -->");
    }

    html.AppendLine(@"</div>");
    html.AppendLine(GetHtmlFooter());

    await File.WriteAllTextAsync(Path.Combine(_outputDirectory, $"sketchbook{lastSketchbookNumber}.html"), html.ToString());
    #endregion  
    
    }

  */

  private async Task GenerateArtworkListPageOld()
  {
    // URL templates for S3 images
    const string S3_MAIN_JPG_URL = "https://keithlong-art-photos.s3.us-east-1.amazonaws.com/jpg/{0}.jpg";
    const string S3_MAIN_TIF_URL = "https://keithlong-art-photos.s3.us-east-1.amazonaws.com/{0}.tif";
    const string S3_ARTWORK_IMAGE_URL = "https://keithlong-art-photos.s3.us-east-1.amazonaws.com/atch/artwork_{0}_{1}.jpg";

    await using var connection = new NpgsqlConnection(_connectionString);
    await connection.OpenAsync();

    var sql = @"
            SELECT
                a.id_field,
                a.iFileName,
                a.title,
                a.series,
                a.create_dt,
                a.medium,
                a.dimensions,
                a.FOLDED_DIMENSIONS,
                a.location,
                a.notes,
                a.human_readable_id,
                a.artwork_image_id,
                ai_back.id_field as back_id,
                ai_front.id_field as front_id,
                ai_paper.id_field as paper_id,
                ai_polaroid.id_field as polaroid_id
            FROM artwork a
            LEFT JOIN artwork_image ai_back ON a.airtable_id = ai_back.artwork_id AND ai_back.view LIKE 'Back%'
            LEFT JOIN artwork_image ai_front ON a.airtable_id = ai_front.artwork_id AND ai_front.view LIKE 'Front%'
            LEFT JOIN artwork_image ai_paper ON a.airtable_id = ai_paper.artwork_id AND ai_paper.view LIKE 'Paper%'
            LEFT JOIN artwork_image ai_polaroid ON a.airtable_id = ai_polaroid.artwork_id AND ai_polaroid.view LIKE 'Polaroid%'
            ORDER BY a.human_readable_id ASC NULLS LAST";

    await using var cmd = new NpgsqlCommand(sql, connection);
    await using var reader = await cmd.ExecuteReaderAsync();

    var html = new StringBuilder();
    html.AppendLine(GetHtmlHeader("All Artworks - Keith Long Archive"));

    html.AppendLine(@"
    <div class='container'>
        <h1>All Artworks</h1>
        <p class='subtitle'><a href='index.html'>← Back to Home</a></p>
        ");

    html.AppendLine("<div class='gallery' style='font-size: x-small;'>");
    while (await reader.ReadAsync())
    {
      var id = reader.IsDBNull(0) ? "" : reader.GetInt32(0).ToString();
      var iFileName = reader.IsDBNull(1) ? "" : reader.GetString(1);
      var title = reader.IsDBNull(2) ? "" : reader.GetString(2);
      var series = reader.IsDBNull(3) ? "" : reader.GetString(3);
      var ctDate = reader.IsDBNull(4) ? "" : reader.GetDateTime(4).ToString("yyyy-MM-dd");
      var medium = reader.IsDBNull(5) ? "" : reader.GetString(5);
      var dimensions = reader.IsDBNull(6) ? "" : reader.GetString(6);
      var foldDimensions = reader.IsDBNull(7) ? "" : reader.GetString(7);
      var location = reader.IsDBNull(8) ? "" : reader.GetString(8);
      var notes = reader.IsDBNull(9) ? "" : reader.GetString(9);
      var humanId = reader.IsDBNull(10) ? "" : reader.GetString(10);
      var image_ids = reader.IsDBNull(11) ? "" : reader.GetString(11);
      var backId = reader.IsDBNull(12) ? (int?)null : reader.GetInt32(12);
      var frontId = reader.IsDBNull(13) ? (int?)null : reader.GetInt32(13);
      var paperId = reader.IsDBNull(14) ? (int?)null : reader.GetInt32(14);
      var polaroidId = reader.IsDBNull(15) ? (int?)null : reader.GetInt32(15);

      bool haveImg = !string.IsNullOrEmpty(iFileName);
      var tifURL = haveImg ? string.Format(S3_MAIN_TIF_URL, iFileName) : "";
      var imgURL = haveImg ? string.Format(S3_MAIN_JPG_URL, iFileName) : "";

      // If no main image but we have a Front large image, use that as fallback
      if (!haveImg && frontId.HasValue)
      {
        imgURL = string.Format(S3_ARTWORK_IMAGE_URL, frontId.Value, "large");
        haveImg = true;
      }

      html.AppendLine($@"<div class='gallery-item'>");
      if (haveImg)
      {
        html.AppendLine($@"  <a href='{imgURL}' target='_blank' rel='noopener noreferrer'>
                     <img src='{imgURL}' title='(click for full size)'/>
                    </a><br/>
                    <div class='desc'><a class='desc' href='{tifURL}'>[tif file]</a></div>");

        // Add thumbnail buttons for additional views
        var thumbnails = new List<(string label, int? id)>
        {
          ("Back", backId),
          ("Front", frontId),
          ("Paper", paperId),
          ("Polaroid", polaroidId)
        };

        var thumbButtons = thumbnails
          .Where(t => t.id.HasValue)
          .Select(t =>
          {
            var thumbUrl = string.Format(S3_ARTWORK_IMAGE_URL, t.id!.Value, "small");
            var largeUrl = string.Format(S3_ARTWORK_IMAGE_URL, t.id!.Value, "large");
            return $"<a href='{largeUrl}' target='_blank' rel='noopener noreferrer' class='thumb-button' title='{t.label} view'><img src='{thumbUrl}' width='36' height='36' /><img src='{largeUrl}' class='thumb-preview' /></a>";
          })
          .ToList();

        if (thumbButtons.Any())
        {
          html.AppendLine($"  <div class='thumb-buttons'>");
          html.AppendLine($"    {string.Join(" ", thumbButtons)}");
          html.AppendLine($"  </div>");
        }
      }
      html.AppendLine($"  <div class='desc'>");
      html.AppendLine($"    {BlankOrWithBR(title, "  ")}");
      html.AppendLine($"    {BlankOrWithBR(ctDate, "  ")}");
      html.AppendLine($"    {BlankOrWithBR(medium, "  ")}");
      html.AppendLine($"    {BlankOrWithBR(dimensions, "  ")}");
      html.AppendLine($"    {BlankOrWithBR(foldDimensions, "   Folded: ")}");
      html.AppendLine($"    {BlankOrWithBR(notes, "  Notes: ")}");
      html.AppendLine($"    {BlankOrWithBR(humanId, "  ")}");
      html.AppendLine($"  </div>");

      html.AppendLine($"</div>  <!-- gallery item -->");
    }

    html.AppendLine(@"</div>");
    html.AppendLine(GetHtmlFooter());

    await File.WriteAllTextAsync(Path.Combine(_outputDirectory, "artworks.html"), html.ToString());
  }

  private async Task GenerateSeriesPages()
  {
    await using var connection = new NpgsqlConnection(_connectionString);
    await connection.OpenAsync();

    var seriesSql = @"
            SELECT
                series,
                COUNT(*) as count,
                MIN(create_dt) as first_date,
                MAX(create_dt) as last_date
            FROM artwork
            WHERE series IS NOT NULL AND series != ''
            GROUP BY series
            ORDER BY count DESC";

    var html = new StringBuilder();
    html.AppendLine(GetHtmlHeader("Artworks by Series - Keith Long Archive"));
    html.AppendLine(@"
    <div class='container'>
        <h1>Artworks by Series</h1>
        <p class='subtitle'><a href='index.html'>← Back to Home</a></p>

        <div class='series-grid'>");

    await using var cmd = new NpgsqlCommand(seriesSql, connection);
    await using var reader = await cmd.ExecuteReaderAsync();

    while (await reader.ReadAsync())
    {
      var series = reader.GetString(0);
      var count = reader.GetInt32(1);
      var firstDate = reader.IsDBNull(2) ? null : reader.GetDateTime(2).ToString("yyyy");
      var lastDate = reader.IsDBNull(3) ? null : reader.GetDateTime(3).ToString("yyyy");
      var dateRange = (firstDate == lastDate) ? firstDate : $"{firstDate} - {lastDate}";

      html.AppendLine($@"
            <div class='series-card'>
                <h3>{EscapeHtml(series)}</h3>
                <div class='series-count'>{count} artwork{(count != 1 ? "s" : "")}</div>
                <div class='series-date'>{dateRange}</div>
            </div>");
    }

    html.AppendLine(@"
        </div>
    </div>");
    html.AppendLine(GetHtmlFooter());

    await File.WriteAllTextAsync(Path.Combine(_outputDirectory, "series.html"), html.ToString());
  }

  private async Task GenerateLocationPages()
  {
    await using var connection = new NpgsqlConnection(_connectionString);
    await connection.OpenAsync();

    var locationSql = @"
            SELECT
                location,
                COUNT(*) as count
            FROM artwork
            WHERE location IS NOT NULL AND location != ''
            GROUP BY location
            ORDER BY count DESC";

    var html = new StringBuilder();
    html.AppendLine(GetHtmlHeader("Artworks by Location - Keith Long Archive"));
    html.AppendLine(@"
    <div class='container'>
        <h1>Artworks by Location</h1>
        <p class='subtitle'><a href='index.html'>← Back to Home</a></p>

        <div class='location-grid'>");

    await using var cmd = new NpgsqlCommand(locationSql, connection);
    await using var reader = await cmd.ExecuteReaderAsync();

    while (await reader.ReadAsync())
    {
      var location = reader.GetString(0);
      var count = reader.GetInt32(1);

      html.AppendLine($@"
            <div class='location-card'>
                <h3>{EscapeHtml(location)}</h3>
                <div class='location-count'>{count} artwork{(count != 1 ? "s" : "")}</div>
            </div>");
    }

    html.AppendLine(@"
        </div>
    </div>");
    html.AppendLine(GetHtmlFooter());

    await File.WriteAllTextAsync(Path.Combine(_outputDirectory, "locations.html"), html.ToString());
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

.stats-table {
    width: 100%;
    border-collapse: collapse;
    margin-top: 12px;
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
    border-bottom: 1px solid #eee;
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

.series-grid, .location-grid {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(250px, 1fr));
    gap: 20px;
    margin: 30px 0;
}

.series-card, .location-card {
    background: white;
    padding: 25px;
    border-radius: 8px;
    box-shadow: 0 2px 4px rgba(0,0,0,0.1);
    transition: transform 0.2s, box-shadow 0.2s;
}

.series-card:hover, .location-card:hover {
    transform: translateY(-5px);
    box-shadow: 0 4px 8px rgba(0,0,0,0.15);
}

.series-card h3, .location-card h3 {
    color: #2c3e50;
    margin-bottom: 10px;
    font-size: 1.3em;
}

.series-count, .location-count {
    color: #3498db;
    font-weight: 600;
    font-size: 1.1em;
    margin-bottom: 5px;
}

.series-date {
    color: #7f8c8d;
    font-size: 0.9em;
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
