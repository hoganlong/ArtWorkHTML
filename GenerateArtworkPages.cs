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
             ai.view LIKE 'Polaroid%')  -- uploaded Polaroid is not used anymore should be removed.
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
          diri_polaroid.dirimgs AS polaroid_imgs,  -- 18
          t.code, t.description as type_desc, -- 19, 20
          a.unsigned -- 21
        FROM artwork a
        LEFT JOIN artwork_type t ON a.type_id ->> 0 = t.airtable_id
        LEFT JOIN imgGroup ai_back ON a.airtable_id = ai_back.artwork_id AND ai_back.lview    ='Back'
        LEFT JOIN imgGroup ai_front ON a.airtable_id = ai_front.artwork_id AND ai_front.lview ='Fron'
        LEFT JOIN imgGroup ai_paper ON a.airtable_id = ai_paper.artwork_id AND ai_paper.lview ='Pape'
        LEFT JOIN imgGroup ai_polaroid ON a.airtable_id = ai_polaroid.artwork_id AND ai_polaroid.lview ='Pola'  -- not used anymore should be removed
        LEFT JOIN dirimgGroup diri_back ON a.airtable_id = diri_back.artwork_id AND diri_back.lview    ='Back'
        LEFT JOIN dirimgGroup diri_front ON a.airtable_id = diri_front.artwork_id AND diri_front.lview ='Fron'
        LEFT JOIN dirimgGroup diri_polaroid ON a.airtable_id = diri_polaroid.artwork_id AND diri_polaroid.lview ='Pola'
        ORDER BY a.human_readable_id, a.create_dt ASC NULLS last";

    const string photoSQL = @"
       SELECT p.file_location, p.human_readable_id
       FROM photo p
       WHERE p.file_location IS NOT NULL";

    const string sketchSQL = @"
       SELECT s.airtable_id, s.sketch_dt, s.description, s.sketch_loc, s.sketch_people,
              s.sketch_medium, s.sketchbook_number, s.page_number, s.artwork_id, s.filename, s.pub_notes,
              s.hide, s.deletefile
       FROM sketch s
       ORDER BY s.sketchbook_number ASC, s.page_number ASC";

  private async Task GenerateArtworkPages()
  {
    // URL templates for S3 images

    ArtList artList = new();
    ArtList polaroidList = new();
    ArtList sketchBookList = new();
    ArtList scansList = new();
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
      var polaroidFileName = reader.IsDBNull(18) ? null : reader.GetString(18).Split(',').Select(s => s.Trim()).ToArray();


      Artwork artwork = new(reader.GetInt32(0).ToString(), reader.IsDBNull(1) ? "" : reader.GetString(1),
         reader.IsDBNull(2) ? "" : reader.GetString(2), reader.IsDBNull(3) ? "" : reader.GetString(3),
          reader.IsDBNull(4) ? DateTime.MinValue : reader.GetDateTime(4), reader.IsDBNull(5) ? "" : reader.GetString(5),
          reader.IsDBNull(6) ? "" : reader.GetString(6), reader.IsDBNull(7) ? "" : reader.GetString(7),
          reader.IsDBNull(8) ? "" : reader.GetString(8), reader.IsDBNull(9) ? "" : reader.GetString(9),
          reader.IsDBNull(10) ? "" : reader.GetString(10), reader.IsDBNull(11) ? "" : reader.GetString(11),
          reader.IsDBNull(19) ? "" : reader.GetString(19),
          backId, frontId, paperId, polaroidId,backFileName, frontFileName, polaroidFileName);
      artwork.unsigned = !reader.IsDBNull(21) && reader.GetBoolean(21);

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
      sketch.deletefile = !sketchreader.IsDBNull(12) && sketchreader.GetBoolean(12);

      sketchBookList.AddArtwork(sketch);
    } // while reader.ReadAsync()
    sketchreader.Close();
    sketchcmd.Dispose();

    // Load photo records so bucket scan can match them without creating noDB entries
    await using var photocmd = new NpgsqlCommand(photoSQL, connection);
    await using var photoreader = await photocmd.ExecuteReaderAsync();

    while (await photoreader.ReadAsync())
    {
      string fileLocation = photoreader.GetString(0);
      string humanReadableId = photoreader.IsDBNull(1) ? fileLocation : photoreader.GetString(1);

      Artwork photo = new("", fileLocation, true);
      photo.myType = ArtType.NonArtPhoto;
      photo.humanId = humanReadableId;
      artList.AddArtwork(photo);
    }
    photoreader.Close();
    photocmd.Dispose();

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
      int sscanBucketFiles = 0;
      int sscanJPGBucketFiles = 0;
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

          if (fullPath.EndsWith("/")) continue;
          if (ext == "pdf") { skippedBucketFiles++; continue; }

          switch (dir)
          {
            case "scans":
              scanBucketFiles++;
              // If the DB has an artwork referencing this scan file
              // (FileName = "scans/<name>"), treat it as a regular artwork
              // hit and skip sketchbook/polaroid/scans-page categorisation.
              if (artList.TryAttachBucketFile(name, ext, "scans/") != null) break;
              if (ext == "tif" && filename.StartsWith("KLA"))
              {
                if (!DbSketchOnly)
                  sketchBookList.AddSketchBucketFile("scans/", name, ext, lastModified, true);
              }
              else if (System.Text.RegularExpressions.Regex.IsMatch(name, @"^\d{4}P\d+$"))
                polaroidList.AddBucketFile("scans/", name, ext, lastModified, true);
              else
                scansList.AddBucketFile("scans/", name, ext, lastModified, true);
              break;

            case "scans/jpg":
              scanJPGBucketFiles++;
              if (artList.TryAttachBucketFile(name, ext, "scans/") != null) break;
              if (filename.StartsWith("KLA"))
              {
                if (!DbSketchOnly)
                  sketchBookList.AddSketchBucketFile("scans/", name, ext, lastModified, true);
              }
              else if (System.Text.RegularExpressions.Regex.IsMatch(name, @"^\d{4}P\d+$"))
                polaroidList.AddBucketFile("scans/", name, ext, lastModified, true);
              else
                scansList.AddBucketFile("scans/", name, ext, lastModified, true);
              break;

            case "sscan":
              sscanBucketFiles++;
              // Same logic as "scans" but with dbPrefix "sscan/". sscan/jpg uses the
              // three-size naming, so the scansList entry's jpgURL must point at
              // <base>_large.jpg, not <base>.jpg (which is what AddBucketFile sets by default).
              if (artList.TryAttachBucketFile(name, ext, "sscan/") != null) break;
              if (ext == "tif" && filename.StartsWith("KLA"))
              {
                if (!DbSketchOnly)
                  sketchBookList.AddSketchBucketFile("sscan/", name, ext, lastModified, true);
              }
              else if (System.Text.RegularExpressions.Regex.IsMatch(name, @"^\d{4}P\d+$"))
                polaroidList.AddBucketFile("sscan/", name, ext, lastModified, true);
              else
              {
                var a = scansList.AddBucketFile("sscan/", name, ext, lastModified, true);
                if (ext == "tif") a.SetSizedJpgURL("sscan/", name);
              }
              break;

            case "sscan/jpg":
              sscanJPGBucketFiles++;
              // sscan JPGs come in three variants: <base>_small.jpg, _large.jpg, _full.jpg.
              // Strip the suffix before matching against DB FileName values.
              string sscanBase = StripSizeSuffix(name, out string? sscanSize);
              if (artList.TryAttachBucketFile(sscanBase, ext, "sscan/") != null) break;
              // Only the _large variant triggers fallback categorisation so we don't
              // emit three duplicate rows per basename. (Files without a known size
              // suffix also fall through — likely a naming bug to surface in scans.html.)
              if (sscanSize != null && sscanSize != "_large") break;
              if (sscanBase.StartsWith("KLA"))
              {
                if (!DbSketchOnly)
                  sketchBookList.AddSketchBucketFile("sscan/", sscanBase, ext, lastModified, true);
              }
              else if (System.Text.RegularExpressions.Regex.IsMatch(sscanBase, @"^\d{4}P\d+$"))
                polaroidList.AddBucketFile("sscan/", sscanBase, ext, lastModified, true);
              else
              {
                var a = scansList.AddBucketFile("sscan/", sscanBase, ext, lastModified, true);
                a.SetSizedJpgURL("sscan/", sscanBase);
              }
              break;

            case "jpg":
              if (ext == "jpg")
                artList.AddBucketFile("", name, ext, lastModified);
              else
              {
                Console.WriteLine($"Expected jpg extension but found: {ext} in file: {fullPath}");
                skippedBucketFiles++;
              }
              break;

            case "atch":
              atchBucketFiles++;
              break;

            case "jpg_hd":
              break;

            case "":
              if (ext == "tif")
                artList.AddBucketFile(dir, name, ext, lastModified);
              break;

            default:
              Console.WriteLine($"Unknown directory: {dir} in file: {fullPath}");
              break;
          }
        }
        request.ContinuationToken = response.NextContinuationToken;
      } while (response.IsTruncated == true);

      Console.WriteLine($"Total files in bucket: {totalBucketFiles}");
      Console.WriteLine($"Total JPG files in bucket: {JPGBucketFiles}");
      Console.WriteLine($"Total scan JPG files in bucket: {scanJPGBucketFiles}");
      Console.WriteLine($"Total scan files in bucket: {scanBucketFiles}");
      Console.WriteLine($"Total sscan JPG files in bucket: {sscanJPGBucketFiles}");
      Console.WriteLine($"Total sscan files in bucket: {sscanBucketFiles}");
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

    // Generate the all-artworks page plus one filtered page per artwork type.
    // Error pre-pass: missing front/back photo flags on each artwork. Done
    // once so the same art.errors collection is reused across the all-artworks
    // page and the per-type pages without double-counting.
    foreach (var artItem in artList.artworks)
    {
      Artwork art = artItem.Value;
      if (art.myType == ArtType.NonArtPhoto) continue;

      if (art.typeCode == "W" &&
          (art.backId == null || art.backId.Length == 0) &&
          (art.backFileName == null || art.backFileName.Length == 0))
        art.errors.Add("Missing back photo");

      if (string.IsNullOrEmpty(art.fileName) &&
          (art.frontId == null || art.frontId.Length == 0) &&
          (art.frontFileName == null || art.frontFileName.Length == 0))
        art.errors.Add("Missing front photo");
    }

    _polaroidCount = polaroidList.artworks.Count;
    _scansCount = scansList.artworks.Count;
    _dateNotEnteredCount = artList.artworks.Values.Count(a => a.ctDate.Year == 1899);
    _dateUnknownCount = artList.artworks.Values.Count(a => a.ctDate.Year == 1900);

    await WriteArtworkGalleryPage(
      "artwork.html",
      "All Artworks",
      artList.artworks.Values,
      includeTypeFilter: true,
      trackErrors: true);

    foreach (var typePage in ArtworkTypePages)
    {
      var filtered = artList.artworks.Values
        .Where(a => !string.IsNullOrEmpty(a.typeCode) && typePage.TypeCodes.Contains(a.typeCode));
      await WriteArtworkGalleryPage(
        typePage.FileName,
        $"Artwork - {typePage.Title}",
        filtered,
        includeTypeFilter: false,
        trackErrors: false);
      Console.WriteLine($"  ✓ {typePage.FileName}");
    }

    var html = new StringBuilder();

    #region polaroid list page
    html.Clear();

    // Now generate the HTML page for the polaroid list
    html.AppendLine(GetHtmlHeader("{Polaroids - Keith Long Archive"));

    html.AppendLine(@"
    <div class='container'>
        <h1>Polaroids</h1>
        <p class='subtitle'><a id='back-link' href='index.html'>← Back to Home</a></p>
    </div>
    <div class='page-controls'>
        <span class='page-controls-label'>Hover effects:</span>
        <label><input type='checkbox' id='chk-image-hover' checked onchange='document.body.classList.toggle(""no-image-hover"", !this.checked)'> Image zoom (z)</label>
    </div>");
    html.AppendLine($"<script>{GetTagsScript()}</script>");
    html.AppendLine(@"<script>
    document.addEventListener('keydown', function(e) {
        if (e.target.tagName === 'INPUT' || e.target.tagName === 'TEXTAREA') return;
        if (e.key === 'z' || e.key === 'Z') document.getElementById('chk-image-hover')?.click();
        if (e.key === 'p' || e.key === 'P') document.getElementById('chk-thumb-hover')?.click();
        if (e.key === 't' || e.key === 'T') window.scrollTo({ top: 0, behavior: 'smooth' });
    });
    </script>
    <div id='tag-title' class='tag-title-banner' style='display:none'></div>
    <div class='container'>");

    html.AppendLine("<div class='gallery' style='font-size: x-small;'>");
    foreach (var artItem in polaroidList.artworks) // while (await reader.ReadAsync())
    {
      Artwork art = artItem.Value;

      html.AppendLine($@"<div class='gallery-item'>");
      html.AppendLine($@"  <a href='{art.jpgFullURL}' rel='noopener noreferrer'><img src='{art.jpgURL}' title='(click for full size)' loading='lazy'/></a><br/>
        <div class='desc'><a class='desc' href='{art.tifURL}'>[tif file]</a></div>");

      html.AppendLine($"<div>");
      html.AppendLine($"  <div class='desc item-description'>");
      html.AppendLine($"    {BlankOrWithBR(art.title, "  ")}");
      html.AppendLine($"    {BlankOrWithBR(DateOrEmpty(art.ctDate), "  ")}");
      html.AppendLine($"    {BlankOrWithBR(art.medium, "  ")}");
      html.AppendLine($"    {BlankOrWithBR(art.dimensions, "  "," inches")}");
      html.AppendLine($"    {BlankOrWithBR(art.foldedDimensions, "   Folded: "," inches")}");
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
    html.AppendLine(GetLightboxHtml());
    html.AppendLine(GetLightboxScriptTag());
    html.AppendLine(GetHtmlFooter());

    await File.WriteAllTextAsync(Path.Combine(_outputDirectory, "polaroids.html"), html.ToString());
    #endregion

    await GenerateScansPage(scansList);

    #region sketchbook list pages
    var lastSketchbookNumber = -1;
    List<int> sketchbookNumbers = sketchBookList.artworks.Values.Where(e => e.myType.HasFlag(ArtType.Sketch) && !e.hide && !e.deletefile).Select(a => a.sketchbookNumber).Distinct().OrderBy(n => n).ToList();

    // Ensure sketchbooks subdirectory exists
    var sketchbooksDir = Path.Combine(_outputDirectory, "sketchbooks");
    Directory.CreateDirectory(sketchbooksDir);

     // Now generate the HTML page for each sketchbook list
    foreach (var sketchBookEntry in sketchBookList.artworks.Where(e => e.Value.myType.HasFlag(ArtType.Sketch) && !e.Value.hide && !e.Value.deletefile).OrderBy(e => e.Value.sketchbookNumber).ThenBy(e => e.Value.pageNumber))
    {
      int bookNumber = sketchBookEntry.Value.sketchbookNumber;

      if (bookNumber != lastSketchbookNumber)  // We've hit a new sketchbook, so we need to start a new HTML page (but first write out the previous one if this isn't the first sketchbook)
      {
        if (lastSketchbookNumber != -1)
        {
          // If this is not the first sketchbook, we need to write out the previous one before starting a new one
          html.AppendLine(@"</div>"); // close container
          html.AppendLine(GetLightboxHtml());
          html.AppendLine(GetLightboxScriptTag("../"));
          html.AppendLine(GetHtmlFooter("../"));
          await File.WriteAllTextAsync(Path.Combine(sketchbooksDir, $"sketchbook{lastSketchbookNumber}.html"), html.ToString());
          Console.WriteLine($"  ✓ sketchbooks/sketchbook{lastSketchbookNumber}.html");
        }
        lastSketchbookNumber = bookNumber;
        html.Clear();

        // Now generate the HTML page for the sketchbook list
        html.AppendLine(GetHtmlHeader($"Sketchbook {bookNumber} - Keith Long Archive", "../"));

        html.AppendLine(@"
        <div class='container'>
          <h1>Sketchbooks</h1>
          <p class='subtitle'><a id='back-link' href='../index.html'>← Back to Home</a></p>");

        // Add navigation for other sketchbooks if there are multiple
        if (sketchbookNumbers.Count > 1)
        {
          html.AppendLine("<div class='sketchbook-nav'><span class='sketchbook-nav-label'>Sketchbook:</span>");
          foreach (var entry in sketchbookNumbers)
          {
            if (entry == bookNumber)
              html.AppendLine($"<span class='nav-button sketchbook-nav-button active'>{entry}</span>");
            else
              html.AppendLine($"<a href='sketchbook{entry}.html?show=all' class='nav-button sketchbook-nav-button'>{entry}</a>");
          }
          html.AppendLine("</div>");
        }

        html.AppendLine(@"
        </div>
        <div class='page-controls'>
            <span class='page-controls-label'>Hover effects:</span>
            <label><input type='checkbox' id='chk-image-hover' checked onchange='document.body.classList.toggle(""no-image-hover"", !this.checked)'> Image zoom (z)</label>
        </div>");
        html.AppendLine($"<script>{GetTagsScript()}</script>");
        html.AppendLine(@"<script>
          document.addEventListener('keydown', function(e) {
            if (e.target.tagName === 'INPUT' || e.target.tagName === 'TEXTAREA') return;
            if (e.key === 'z' || e.key === 'Z') document.getElementById('chk-image-hover')?.click();
            if (e.key === 'p' || e.key === 'P') document.getElementById('chk-thumb-hover')?.click();
            if (e.key === 't' || e.key === 'T') window.scrollTo({ top: 0, behavior: 'smooth' });
          });
        </script>
        <div id='tag-title' class='tag-title-banner' style='display:none'></div>
        <div class='container'>");

        html.AppendLine("<div class='gallery' style='font-size: x-small;'>");
      }

      Artwork art = sketchBookEntry.Value;

      html.AppendLine($@"<div class='gallery-item'>");
      html.AppendLine($@"  <a href='{art.jpgFullURL}' rel='noopener noreferrer'><img src='{art.jpgURL}' title='(click for full size)' loading='lazy'/></a><br/>
        <div class='desc'><a class='desc' href='{art.tifURL}'>[tif file]</a></div>");

      html.AppendLine($"  <div class='desc item-description'>");
      html.AppendLine($"    {BlankOrWithBR(art.pageNumber.ToString(), " ")}");
      html.AppendLine($"    {BlankOrWithBR(DateOrEmpty(art.ctDate), "  ")}");

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
      html.AppendLine($"  <my-hidden-tags>B{art.sketchbookNumber}_P{art.pageNumber}</my-hidden-tags>");

      html.AppendLine($"<div class='desc' style='color:red;'>{String.Join("<br/>", art.errors)}</div>");

      if (art.states.HasFlag(StatesType.jpgFound))
        html.AppendLine($"<span class='heavy-check-mark'>&#x2705;</span>");
      if (art.states.HasFlag(StatesType.tifFound))
        html.AppendLine($"<span class='heavy-check-mark'>&#x2705;</span>");

      html.AppendLine($"</div>  <!-- gallery item -->");
    }

    html.AppendLine(@"</div>");
    html.AppendLine(GetLightboxHtml());
    html.AppendLine(GetLightboxScriptTag("../"));
    html.AppendLine(GetHtmlFooter("../"));

    await File.WriteAllTextAsync(Path.Combine(sketchbooksDir, $"sketchbook{lastSketchbookNumber}.html"), html.ToString());
    Console.WriteLine($"  ✓ sketchbooks/sketchbook{lastSketchbookNumber}.html");

    // Generate sketchbooks.html index at root
    html.Clear();
    html.AppendLine(GetHtmlHeader("Sketchbooks - Keith Long Archive"));
    html.AppendLine(@"
    <div class='container landing-page'>
      <div class='landing-header'>
        <h1>Sketchbooks</h1>
        <p class='subtitle'><a id='back-link' href='index.html'>← Back to Home</a></p>
      </div>
      <div class='landing-content'>
        <p>Keith Long kept sketchbooks throughout his career, filling them with drawings, studies,
           and observations made on location and in the studio. Each book is a record of a period
           of his life and work. Browse any sketchbook below.</p>
      </div>
      <div class='navigation'>");
    foreach (var n in sketchbookNumbers)
      html.AppendLine($"<div class='nav-button-wrap'><a href='sketchbooks/sketchbook{n}.html?show=all' class='nav-button'>Sketchbook {n}</a><div class='coming-soon'>&nbsp;</div></div>");
    html.AppendLine(@"
      </div>
    </div>");
    html.AppendLine(GetHtmlFooter());
    await File.WriteAllTextAsync(Path.Combine(_outputDirectory, "sketchbooks.html"), html.ToString());
    Console.WriteLine("  ✓ sketchbooks.html - Sketchbook index");

    #endregion

    #region hidden sketchbook pages
    var lastHideSketchbookNumber = -1;
    List<int> hideSketchbookNumbers = sketchBookList.artworks.Values.Where(e => e.myType.HasFlag(ArtType.Sketch) && !e.deletefile && (e.hide || e.states.HasFlag(StatesType.noDB))).Select(a => a.sketchbookNumber).Distinct().OrderBy(n => n).ToList();

    if (hideSketchbookNumbers.Count > 0)
    {
      // Ensure hide subdirectory exists
      var hideDir = Path.Combine(_outputDirectory, "hide");
      Directory.CreateDirectory(hideDir);

      // Now generate the HTML page for each hidden sketchbook
      foreach (var sketchBookEntry in sketchBookList.artworks.Where(e => e.Value.myType.HasFlag(ArtType.Sketch) && !e.Value.deletefile && (e.Value.hide || e.Value.states.HasFlag(StatesType.noDB))).OrderBy(e => e.Value.sketchbookNumber).ThenBy(e => e.Value.pageNumber))
      {
        int bookNumber = sketchBookEntry.Value.sketchbookNumber;

        if (bookNumber != lastHideSketchbookNumber)
        {
          if (lastHideSketchbookNumber != -1)
          {
            html.AppendLine(@"</div>"); // close container
            html.AppendLine(GetLightboxHtml());
            html.AppendLine(GetLightboxScriptTag("../"));
            html.AppendLine(GetHtmlFooter("../"));
            await File.WriteAllTextAsync(Path.Combine(hideDir, $"sketchbook{lastHideSketchbookNumber}.html"), html.ToString());
            Console.WriteLine($"  ✓ hide/sketchbook{lastHideSketchbookNumber}.html");
          }
          lastHideSketchbookNumber = bookNumber;
          html.Clear();

          html.AppendLine(GetHtmlHeader($"Sketchbook {bookNumber} (Hidden) - Keith Long Archive", "../"));

          html.AppendLine(@"
          <div class='container'>
            <h1>Sketchbooks (Hidden)</h1>
            <p class='subtitle'><a id='back-link' href='../index.html'>← Back to Home</a></p>");

          if (hideSketchbookNumbers.Count > 1)
          {
            html.AppendLine("<div class='sketchbook-nav'><span class='sketchbook-nav-label'>Sketchbook:</span>");
            foreach (var entry in hideSketchbookNumbers)
            {
              if (entry == bookNumber)
                html.AppendLine($"<span class='nav-button sketchbook-nav-button active'>{entry}</span>");
              else
                html.AppendLine($"<a href='sketchbook{entry}.html?show=all' class='nav-button sketchbook-nav-button'>{entry}</a>");
            }
            html.AppendLine("</div>");
          }

          html.AppendLine(@"
          </div>
          <div class='page-controls'>
              <span class='page-controls-label'>Hover effects:</span>
              <label><input type='checkbox' id='chk-image-hover' checked onchange='document.body.classList.toggle(""no-image-hover"", !this.checked)'> Image zoom (z)</label>
          </div>");
          html.AppendLine($"<script>{GetTagsScript()}</script>");
          html.AppendLine(@"<script>
            document.addEventListener('keydown', function(e) {
              if (e.target.tagName === 'INPUT' || e.target.tagName === 'TEXTAREA') return;
              if (e.key === 'z' || e.key === 'Z') document.getElementById('chk-image-hover')?.click();
              if (e.key === 'p' || e.key === 'P') document.getElementById('chk-thumb-hover')?.click();
              if (e.key === 't' || e.key === 'T') window.scrollTo({ top: 0, behavior: 'smooth' });
            });
          </script>
          <div id='tag-title' class='tag-title-banner' style='display:none'></div>
          <div class='container'>");

          html.AppendLine("<div class='gallery' style='font-size: x-small;'>");
        }

        Artwork art = sketchBookEntry.Value;

        html.AppendLine($@"<div class='gallery-item'>");
        html.AppendLine($@"  <a href='{art.jpgFullURL}' rel='noopener noreferrer'><img src='{art.jpgURL}' title='(click for full size)' loading='lazy'/></a><br/>
          <div class='desc'><a class='desc' href='{art.tifURL}'>[tif file]</a></div>");

        html.AppendLine($"  <div class='desc item-description'>");
        html.AppendLine($"    {BlankOrWithBR(art.pageNumber.ToString(), " ")}");
        html.AppendLine($"    {BlankOrWithBR(DateOrEmpty(art.ctDate), "  ")}");
        html.AppendLine($"    {BlankOrWithBR(art.location, "  ")}");
        html.AppendLine($"    {BlankOrWithBR(art.medium, "  ")}");
        html.AppendLine($"    {BlankOrWithBR(art.people, "  ")}");
        html.AppendLine($"    {BlankOrWithBR(art.notes, "  Notes: ")}");

        html.AppendLine($"{BlankOrComment(art.id, "id: ")}");
        html.AppendLine($"{BlankOrComment(art.artworkID!, "artid: ")}");
        html.AppendLine($"{BlankOrComment(art.humanId, "humandId: ")}");
        html.AppendLine($"{BlankOrComment(art.fileName, "filename: ")}");

        html.AppendLine($"  </div>");
        html.AppendLine($"  <my-hidden-tags>B{art.sketchbookNumber}_P{art.pageNumber}</my-hidden-tags>");

        html.AppendLine($"<div class='desc' style='color:red;'>{String.Join("<br/>", art.errors)}</div>");

        if (art.states.HasFlag(StatesType.jpgFound))
          html.AppendLine($"<span class='heavy-check-mark'>&#x2705;</span>");
        if (art.states.HasFlag(StatesType.tifFound))
          html.AppendLine($"<span class='heavy-check-mark'>&#x2705;</span>");

        html.AppendLine($"</div>  <!-- gallery item -->");
      }

      html.AppendLine(@"</div>");
      html.AppendLine(GetLightboxHtml());
      html.AppendLine(GetLightboxScriptTag("../"));
      html.AppendLine(GetHtmlFooter("../"));

      await File.WriteAllTextAsync(Path.Combine(hideDir, $"sketchbook{lastHideSketchbookNumber}.html"), html.ToString());
      Console.WriteLine($"  ✓ hide/sketchbook{lastHideSketchbookNumber}.html");

      // Generate hide.html index at root
      html.Clear();
      html.AppendLine(GetHtmlHeader("Hidden Sketchbook Pages - Keith Long Archive"));
      html.AppendLine(@"
      <div class='container landing-page'>
        <div class='landing-header'>
          <h1>Hidden Sketchbook Pages</h1>
          <p class='subtitle'><a id='back-link' href='index.html'>← Back to Home</a></p>
        </div>
        <div class='landing-content'>
          <p>These sketchbook pages are marked as hidden and are not shown in the main sketchbook view.</p>
        </div>
        <div class='navigation'>");
      foreach (var n in hideSketchbookNumbers)
        html.AppendLine($"<div class='nav-button-wrap'><a href='hide/sketchbook{n}.html?show=all' class='nav-button'>Sketchbook {n}</a><div class='coming-soon'>&nbsp;</div></div>");
      html.AppendLine(@"
        </div>
      </div>");
      html.AppendLine(GetHtmlFooter());
      await File.WriteAllTextAsync(Path.Combine(_outputDirectory, "hide.html"), html.ToString());
      Console.WriteLine("  ✓ hide.html - Hidden sketchbook pages index");
    }

    #endregion

    }

  // Renders one gallery page (artwork.html or one of the per-type artwork-*.html
  // pages). The all-artworks page sets includeTypeFilter=true to keep its
  // "Show types" checkbox row, and trackErrors=true to populate _errorCounts
  // (and emit the trailing HTML comment summary). Per-type pages opt out of
  // both — they reuse the same art.errors values populated by the pre-pass.
  private async Task WriteArtworkGalleryPage(
    string outputFileName,
    string headingText,
    IEnumerable<Artwork> artworks,
    bool includeTypeFilter,
    bool trackErrors)
  {
    var html = new StringBuilder();
    html.AppendLine(GetHtmlHeader($"{headingText} - Keith Long Archive"));

    html.AppendLine($@"
    <div class='container'>
        <h1 id='page-title'>{EscapeHtml(headingText)}</h1>
        <p class='subtitle'><a id='back-link' href='index.html'>← Back to Home</a></p>
    </div>
    <div class='page-controls'>
        <div class='page-controls-row'>
            <span class='page-controls-label'>Hover effects:</span>
            <label><input type='checkbox' id='chk-thumb-hover' checked onchange='document.body.classList.toggle(""no-thumb-hover"", !this.checked)'> Thumbnail preview (p)</label>
            <label><input type='checkbox' id='chk-image-hover' checked onchange='document.body.classList.toggle(""no-image-hover"", !this.checked)'> Image zoom (z)</label>
        </div>");
    if (includeTypeFilter)
    {
      html.AppendLine(@"        <div class='page-controls-row'>
            <span class='page-controls-label'>Show types:</span>
            <span id='type-filter-checkboxes'></span>
        </div>");
    }
    html.AppendLine(@"    </div>");
    html.AppendLine($"<script>{GetTagsScript()}</script>");

    if (includeTypeFilter)
    {
      html.AppendLine(@"<script>
    document.addEventListener('DOMContentLoaded', function() {
        var items = document.querySelectorAll('.gallery-item');
        var typeSet = new Set();
        items.forEach(function(el) {
            var tagsEl = el.querySelector('my-tags');
            if(!tagsEl) return;
            var firstTag = tagsEl.textContent.split(',')[0].trim();
            if(firstTag) typeSet.add(firstTag);
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

        Array.from(typeSet).sort().forEach(function(tag) {
            var label = document.createElement('label');
            var cb = document.createElement('input');
            cb.type = 'checkbox';
            cb.checked = true;
            cb.dataset.filterType = tag;
            cb.addEventListener('change', filterGallery);
            label.appendChild(cb);
            label.appendChild(document.createTextNode(' ' + tag));
            container.appendChild(label);
        });
        function filterGallery() {
            var hidden = new Set();
            container.querySelectorAll('input[data-filter-type]').forEach(function(cb) {
                if (!cb.checked) hidden.add(cb.dataset.filterType);
            });
            items.forEach(function(el) {
                var tagsEl = el.querySelector('my-tags');
                var firstType = tagsEl ? tagsEl.textContent.split(',')[0].trim() : '';
                el.style.display = (firstType && hidden.has(firstType)) ? 'none' : '';
            });
        }
        // Sync checkboxes with active tags from URL/anchor/cookie
        var tagState = window._tagState;
        if (tagState && !tagState.hasAll && tagState.activeTags.size > 0) {
            var typeCbs = Array.from(container.querySelectorAll('input[data-filter-type]'));
            var hasActiveTypeTag = typeCbs.some(function(cb) { return tagState.activeTags.has(cb.dataset.filterType.toLowerCase()); });
            if (hasActiveTypeTag) {
                allCb.checked = false;
                typeCbs.forEach(function(cb) {
                    cb.checked = tagState.activeTags.has(cb.dataset.filterType.toLowerCase());
                });
                filterGallery();
            } else {
                // Non-type tags active (e.g. year) — uncheck All to signal filtered view, leave type boxes checked
                allCb.checked = false;
            }
        }
    });
    </script>");
    }

    html.AppendLine(@"<script>
    document.addEventListener('keydown', function(e) {
        if (e.target.tagName === 'INPUT' || e.target.tagName === 'TEXTAREA') return;
        if (e.key === 'z' || e.key === 'Z') document.getElementById('chk-image-hover')?.click();
        if (e.key === 'p' || e.key === 'P') document.getElementById('chk-thumb-hover')?.click();
        if (e.key === 't' || e.key === 'T') window.scrollTo({ top: 0, behavior: 'smooth' });
    });
    </script>
    <script>
    function applyThumbSize(img) {
        if (img.naturalWidth > img.naturalHeight * 1.4) {
            img.style.width = Math.min(Math.round(40 * img.naturalWidth / img.naturalHeight), 220) + 'px';
        }
        if (img.naturalHeight > img.naturalWidth * 1.4) {
            var largeSrc = img.dataset.largeSrc;
            if (largeSrc && img.src !== largeSrc) {
                img.src = largeSrc;
                return;
            }
            img.style.height = Math.min(Math.round(40 * img.naturalHeight / img.naturalWidth), 120) + 'px';
        }
    }
    </script>
    <script>
    document.addEventListener('DOMContentLoaded', function() {
        document.querySelectorAll('.thumb-button').forEach(function(btn) {
            btn.addEventListener('mouseenter', function() {
                if (this.querySelector('.thumb-preview')) return;
                var thumbImg = this.querySelector('img[data-large-src]');
                if (!thumbImg) return;
                var src = thumbImg.dataset.largeSrc || thumbImg.src;
                if (!src) return;
                var preview = document.createElement('img');
                preview.className = 'thumb-preview';
                preview.src = src;
                this.appendChild(preview);
            });
        });
    });
    </script>
    <div id='tag-title' class='tag-title-banner' style='display:none'></div>
    <div class='container'>");

    html.AppendLine("<div class='gallery' style='font-size: x-small;'>");
    foreach (var art in artworks)
    {
      if (art.myType == ArtType.NonArtPhoto) continue;

      html.AppendLine($@"<div class='gallery-item'>");
      if ((art.states & StatesType.NoImage) == 0)
      {
        html.AppendLine($@"  <a href='{art.jpgFullURL}' rel='noopener noreferrer'>
                   <img src='{art.jpgURL}' title='(click for full size)' loading='lazy'/>
                    </a><br/>
                    <div class='desc'><a class='desc' href='{art.tifURL}'>[tif file]</a></div>");
      }

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
        ("Back", art.backFileName),
        ("Polaroid", art.polaroidFileName)
      };

      List<string> thumbButtons = [];
      bool hasMult = false;

      foreach (var thumb in thumbnails)
      {
        if (thumb.id is not null && thumb.id.Length > 0)
        {
          hasMult = thumb.id.Length > 1;
          int curNum = 1;
          foreach (var id in thumb.id)
          {
            if (id != 0)
            {
              var thumbUrl = string.Format(S3_ARTWORK_IMAGE_URL, id, "small");
              var fullUrl = string.Format(S3_ARTWORK_IMAGE_URL, id, "full");
              var largeUrl = string.Format(S3_ARTWORK_IMAGE_URL, id, "large");
              thumbButtons.Add($"<a href='{fullUrl}' target='_blank' rel='noopener noreferrer' class='thumb-button' title='{thumb.label}{(hasMult?" "+curNum.ToString(): "")}'><img src='{thumbUrl}' width='40' height='40' data-large-src='{largeUrl}' onload='applyThumbSize(this)' loading='lazy' /></a>");
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
          foreach (var name in filethumb.names)
          {
            if (!string.IsNullOrEmpty(name))
            {
              var url = art.MakeJPGURL(name);
              thumbButtons.Add($"<a href='{url}' target='_blank' rel='noopener noreferrer' class='thumb-button' title='{filethumb.label}(L){(hasMult?" "+curNum.ToString(): "")}'><img src='{url}' width='40' height='40' data-large-src='{url}' onload='applyThumbSize(this)' loading='lazy' /></a>");
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

      html.AppendLine($"  <div class='desc item-description'>");
      html.AppendLine($"    {BlankOrWithBR(art.title, "  ")}");
      html.AppendLine($"    {BlankOrWithBR(DateOrEmpty(art.ctDate), "  ")}");
      html.AppendLine($"    {BlankOrWithBR(art.medium, "  ")}");
      html.AppendLine($"    {BlankOrWithBR(art.dimensions, "  "," inches")}");
      html.AppendLine($"    {BlankOrWithBR(art.foldedDimensions, "   Folded: "," inches")}");
      html.AppendLine($"    {BlankOrWithBR(art.notes, "  Notes: ")}");
      if (art.unsigned)
        html.AppendLine($"    <span style='color:lightcoral;'>This artwork is unsigned</span><br/>");
      html.AppendLine($"  </div>");
      var artTags = string.Join(",", new[] { GetTypeTag(art.typeCode), art.humanId }.Where(t => !string.IsNullOrEmpty(t)));
      var seriesTag = MakeTag(art.series);
      var seriesBtn = string.IsNullOrEmpty(seriesTag) ? "" : $" <button class='small-button series-tag-btn' data-series-tag='{EscapeHtml(seriesTag)}' onclick='window._filterToTag(\"{EscapeHtml(seriesTag)}\")' title='Show whole series'>S</button>";
      html.AppendLine($"  <div class='desc'>Tags: <my-tags>{EscapeHtml(artTags)}</my-tags></div><div class='desc'>{seriesBtn}</div>");
      var hiddenTags = string.Join(",", new[] {
        seriesTag,
        MakeTag(art.location),
        art.ctDate == DateTime.MinValue || art.ctDate.Year == 1900 ? "No-Date" : art.ctDate.Year.ToString(),
        art.errors.Count > 0 ? "error" : ""
      }.Where(t => !string.IsNullOrEmpty(t)));
      if (!string.IsNullOrEmpty(hiddenTags))
        html.AppendLine($"  <my-hidden-tags>{EscapeHtml(hiddenTags)}</my-hidden-tags>");

      html.AppendLine($"<div class='desc' style='color:red;'>{String.Join("<br/>", art.errors)}</div>");

      if (trackErrors)
      {
        foreach (var err in art.errors)
        {
          var key = err.StartsWith("Duplicate humanId") ? "Duplicate humanId" : err;
          _errorCounts[key] = _errorCounts.TryGetValue(key, out int c) ? c + 1 : 1;
        }
      }

      if (art.states.HasFlag(StatesType.noDB))
        html.AppendLine($"<div class='desc'>Bucket name: {art.fileName}<br/></div>");

      if (art.states.HasFlag(StatesType.jpgFound))
        html.AppendLine($"<span class='heavy-check-mark'>&#x2705;</span>");
      if (art.states.HasFlag(StatesType.tifFound))
        html.AppendLine($"<span class='heavy-check-mark'>&#x2705;</span>");

      html.AppendLine($"</div>  <!-- gallery item -->");
    }

    html.AppendLine(@"</div>");
    html.AppendLine(GetLightboxHtml());
    html.AppendLine(GetLightboxScriptTag());
    html.AppendLine(GetHtmlFooter());

    if (trackErrors)
    {
      html.AppendLine("<!--");
      foreach (var line in BuildErrorSummaryLines())
        html.AppendLine(line);
      html.AppendLine("-->");
    }

    await File.WriteAllTextAsync(Path.Combine(_outputDirectory, outputFileName), html.ToString());
  }

  // Strip a trailing _small / _large / _full from a JPG basename.
  // Returns the stripped name and (via out param) the suffix that was removed,
  // or null if the name doesn't end in a known size suffix.
  private static string StripSizeSuffix(string name, out string? sizeSuffix)
  {
    foreach (var size in new[] { "_small", "_large", "_full" })
    {
      if (name.EndsWith(size, StringComparison.OrdinalIgnoreCase))
      {
        sizeSuffix = size;
        return name.Substring(0, name.Length - size.Length);
      }
    }
    sizeSuffix = null;
    return name;
  }
}
