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

public partial class ArtworkHTML
{
  const string S3_ARTWORK_IMAGE_URL = "https://keithlong-art-photos.s3.us-east-1.amazonaws.com/atch/artwork_{0}_{1}.jpg";

  const string artworkSQL = @"

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
        WHERE ai_back.ids IS NOT NULL OR ai_front.ids IS NOT NULL OR ai_paper.ids IS NOT NULL OR ai_polaroid.ids IS NOT NULL OR diri_back.dirimgs IS NOT NULL OR diri_front.dirimgs IS NOT NULL
        ORDER BY a.human_readable_id, a.create_dt ASC NULLS last";

    const string sketchSQL = @"
       SELECT s.airtable_id, s.sketch_dt, s.description, s.sketch_loc, s.sketch_people,
              s.sketch_medium, s.sketchbook_number, s.page_number, s.artwork_id, s.filename, s.pub_notes,
              s.hide
       FROM sketch s
       ORDER BY s.sketchbook_number ASC, s.page_number ASC";

  private async Task GenerateArtworkPages()
  {
    // URL templates for S3 images

    ArtList artList = new();
    ArtList polaroidList = new();
    ArtList sketchBookList = new();
 //   Dictionary<int, ArtList> sketchBookLists = [];

    // Get all artworks from the database
    await using var connection = new NpgsqlConnection(_connectionString);
    await connection.OpenAsync();

    await using var cmd = new NpgsqlCommand(artworkSQL, connection);
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

    await using var sketchcmd = new NpgsqlCommand(sketchSQL, connection);
    await using var sketchreader = await sketchcmd.ExecuteReaderAsync();

    while (await sketchreader.ReadAsync())
    {
      string airtable_id= sketchreader.IsDBNull(0) ? "" : sketchreader.GetString(0);
      DateTime ctDate = sketchreader.IsDBNull(1) ? DateTime.MinValue : sketchreader.GetDateTime(1);
      string description = sketchreader.IsDBNull(2) ? "" : sketchreader.GetString(2);
      string location = sketchreader.IsDBNull(3) ? "" : sketchreader.GetString(3);
      string people = sketchreader.IsDBNull(4) ? "" : sketchreader.GetString(4);
      string medium = sketchreader.IsDBNull(5) ? "" : sketchreader.GetString(5);
      int sketchbookNumber = sketchreader.IsDBNull(6) ? 0 : sketchreader.GetInt32(6);
      int pageNumber = sketchreader.IsDBNull(7) ? 0 : sketchreader.GetInt32(7);
      string? artworkID = sketchreader.IsDBNull(8) ? null : sketchreader.GetString(8);
      string filename = sketchreader.IsDBNull(9) ? "" : sketchreader.GetString(9);
      string pubNotes = sketchreader.IsDBNull(10) ? "" : sketchreader.GetString(10);

      Artwork sketch = new(airtable_id, ctDate, location, people, medium, sketchbookNumber, pageNumber, artworkID, pubNotes, filename);
      sketch.hide = !sketchreader.IsDBNull(11) && sketchreader.GetBoolean(11);

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

          if (ext == "jpg")
          {
            JPGBucketFiles++; 
          }
          if (ext == "tif")
          {
            tifBucketFiles++;
          }  

          if (dir == "scans" && ext == "tif" && filename.StartsWith("KLA")) // It's a sketchbook TIF so add it to the sketchbook list
          {
            sketchBookList.AddSketchBucketFile("scans/", name, ext, lastModified, true);  // add that puppy.
            continue;
          }

          if (fullPath.Length > 10 && fullPath[0..9] == "scans/jpg")
          {
            // not doing anything with scans right now, but want to keep track of how many there are in the bucket
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
              sketchBookList.AddSketchBucketFile("scans/", name, ext, lastModified, true);  // add that puppy.
              continue;
            }
            else  // It's a polaroid image so add it to the polaroid list
            {
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
              }
              continue;
            }
            else
            {
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

      html.AppendLine($"<div>");
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
    List<int> sketchbookNumbers = sketchBookList.artworks.Values.Where(e => e.myType.HasFlag(ArtType.Sketch) && !e.hide).Select(a => a.sketchbookNumber).Distinct().OrderBy(n => n).ToList();

     // Now generate the HTML page for each sketchbook list
    foreach (var sketchBookEntry in sketchBookList.artworks.Where(e => e.Value.myType.HasFlag(ArtType.Sketch) && !e.Value.hide).OrderBy(e => e.Value.sketchbookNumber).ThenBy(e => e.Value.pageNumber))
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
          Console.WriteLine($"  ✓ sketchbook{lastSketchbookNumber}.html");
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
    Console.WriteLine($"  ✓ sketchbook{lastSketchbookNumber}.html");

    #endregion  
    
    }
}
