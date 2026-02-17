#pragma warning disable CA2249 

using Microsoft.Extensions.Configuration;
using Npgsql;
using System.Text;

using Amazon.S3;
using Amazon.S3.Model;
using System.Security.Cryptography.X509Certificates;


namespace ArtWorkHTML;

public class ArtworkHTML
{
  private readonly string _connectionString;
  private readonly string _outputDirectory;

  public ArtworkHTML(string connectionString, string outputDirectory)
  {
    _connectionString = connectionString;
    _outputDirectory = outputDirectory;
  }

  public async Task GenerateAllPages()
  {
    await GenerateIndexPage();
    await GenerateStatisticsPage();
    await GenerateArtworkListPage();
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
            <h2>Browse the Archive</h2>
            <a href='statistics.html' class='nav-button'>📊 Archive Statistics</a>
            <a href='artworksplus.html' class='nav-button'>🖼️ Browse All Artworks</a>
            <a href='series.html' class='nav-button'>📚 View by Series</a>
            <a href='locations.html' class='nav-button'>📍 View by Location</a>
        </div>
    </div>");
    html.AppendLine(GetHtmlFooter());

    await File.WriteAllTextAsync(Path.Combine(_outputDirectory, "index.html"), html.ToString());
  }

  private async Task GenerateStatisticsPage()
  {
    await using var connection = new NpgsqlConnection(_connectionString);
    await connection.OpenAsync();

    var sql = @"
            SELECT
                COUNT(*) as total_artworks,
                COUNT(DISTINCT series) as total_series,
                COUNT(DISTINCT location) as total_locations,
                MIN(create_dt) as earliest_date,
                MAX(create_dt) as latest_date
            FROM artwork
            WHERE create_dt IS NOT NULL";

    await using var cmd = new NpgsqlCommand(sql, connection);
    await using var reader = await cmd.ExecuteReaderAsync();

    int totalArtworks = 0, totalSeries = 0, totalLocations = 0;
    string? earliestDate = null, latestDate = null;

    if (await reader.ReadAsync())
    {
      totalArtworks = reader.GetInt32(0);
      totalSeries = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
      totalLocations = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
      earliestDate = reader.IsDBNull(3) ? null : reader.GetDateTime(3).ToString("yyyy");
      latestDate = reader.IsDBNull(4) ? null : reader.GetDateTime(4).ToString("yyyy");
    }

    var html = new StringBuilder();
    html.AppendLine(GetHtmlHeader("Archive Statistics - Keith Long Archive"));
    html.AppendLine(@"
    <div class='container'>
        <h1>Archive Statistics</h1>
        <p class='subtitle'><a href='index.html'>← Back to Home</a></p>

        <div class='stats-grid'>
            <div class='stat-card'>
                <div class='stat-number'>" + totalArtworks + @"</div>
                <div class='stat-label'>Total Artworks</div>
            </div>
            <div class='stat-card'>
                <div class='stat-number'>" + totalSeries + @"</div>
                <div class='stat-label'>Series</div>
            </div>
            <div class='stat-card'>
                <div class='stat-number'>" + totalLocations + @"</div>
                <div class='stat-label'>Locations</div>
            </div>
            <div class='stat-card'>
                <div class='stat-number'>" + earliestDate + " - " + latestDate + @"</div>
                <div class='stat-label'>Date Range</div>
            </div>
        </div>

        <div class='navigation'>
            <a href='artworksplus.html' class='nav-button'>Browse All Artworks</a>
            <a href='series.html' class='nav-button'>View by Series</a>
            <a href='locations.html' class='nav-button'>View by Location</a>
        </div>
    </div>");
    html.AppendLine(GetHtmlFooter());

    await File.WriteAllTextAsync(Path.Combine(_outputDirectory, "statistics.html"), html.ToString());
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


  private async Task GenerateArtworkListPage()
  {
    // URL templates for S3 images
    const string S3_ARTWORK_IMAGE_URL = "https://keithlong-art-photos.s3.us-east-1.amazonaws.com/atch/artwork_{0}_{1}.jpg";

    ArtList artList = new();

    // Get all artworks from the database
    await using var connection = new NpgsqlConnection(_connectionString);
    await connection.OpenAsync();

    var sql = @"
        SELECT
          a.id_field, a.iFileName, a.title, a.series, a.create_dt, a.medium, a.dimensions, a.FOLDED_DIMENSIONS,
          a.location, a.notes, a.human_readable_id, a.artwork_image_id,
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

    while (await reader.ReadAsync())
    {
      var backId = reader.IsDBNull(12) ? (int?)null : reader.GetInt32(12);
      var frontId = reader.IsDBNull(13) ? (int?)null : reader.GetInt32(13);
      var paperId = reader.IsDBNull(14) ? (int?)null : reader.GetInt32(14);
      var polaroidId = reader.IsDBNull(15) ? (int?)null : reader.GetInt32(15);

      Artwork artwork = new(reader.GetInt32(0).ToString(), reader.IsDBNull(1) ? "" : reader.GetString(1),
         reader.IsDBNull(2) ? "" : reader.GetString(2), reader.IsDBNull(3) ? "" : reader.GetString(3),
          reader.IsDBNull(4) ? DateTime.MinValue : reader.GetDateTime(4), reader.IsDBNull(5) ? "" : reader.GetString(5),
          reader.IsDBNull(6) ? "" : reader.GetString(6), reader.IsDBNull(7) ? "" : reader.GetString(7),
          reader.IsDBNull(8) ? "" : reader.GetString(8), reader.IsDBNull(9) ? "" : reader.GetString(9),
          reader.IsDBNull(10) ? "" : reader.GetString(10), reader.IsDBNull(11) ? "" : reader.GetString(11),
          backId, frontId, paperId, polaroidId);

      artList.AddArtwork(artwork);
    } // while reader.ReadAsync()


    string bucketName = "keithlong-art-photos";
    string region = "us-east-1";
    //    Dictionary<string, int> updates = new();

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
      do
      {
        response = await s3Client.ListObjectsV2Async(request);

        foreach (Amazon.S3.Model.S3Object obj in response.S3Objects)
        {
          totalBucketFiles++;
          string fullPath = obj.Key;

          if (fullPath.Length > 10 && fullPath[0..9] == "scans/jpg")
          {
            // not doing anything with scans right now, but want to keep track of how many there are in the bucket
            scanJPGBucketFiles++;

            /*      string filename = obj.Key.Substring(10);
                  string name = filename.Remove(filename.LastIndexOf('.'));
                  string url = $"https://keithlong-art-photos.s3.us-east-1.amazonaws.com/scans/jpg/{filename}";

                  htmlContent.Append("  <div class= \"gallery-item\" >\n");
                  htmlContent.Append($"    <a href=\"{url}\" target=\"_blank\" rel=\"noopener noreferrer\"><img src=\"{url}\" title=\"(click for full size)\"  /></a>\n");
                  htmlContent.Append($"    <div class=\"desc\">{name}<br/><div style=\"font-size:x-small\">{obj.LastModified}</div></div>\n");
                  htmlContent.Append("  </div>\n");

                  string lastModDate = obj.LastModified.Value.ToShortDateString();
                  if (updates.ContainsKey(lastModDate))
                    updates[lastModDate]++;
                  else
                    updates[lastModDate] = 1;
            */
          }
          else
          {
            // Just the dir ignore it.
            if (fullPath.EndsWith("/"))
            {
              continue;
            }
            int slashPos = fullPath.LastIndexOf('/');
              string dir = slashPos > 0 ? fullPath[0..slashPos]:"";
              string filename = fullPath[(slashPos + 1)..];
              int dotLoc = filename.LastIndexOf('.');
              string name = (dotLoc == -1) ? filename : filename[0..dotLoc];
              string ext = (dotLoc == -1) ? "" : filename[(dotLoc + 1)..].ToLower();

            if (slashPos == -1)
            {
              // tif dir files are in the root of the bucket, so we want to skip those
              artList.AddBucketFile(dir, name, ext);
              JPGBucketFiles++;

              tifBucketFiles++;
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
                    artList.AddBucketFile(dir, name, ext);
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


            //            Console.WriteLine(obj.Key);
            // skippedBucketFiles++;
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

    /*
    foreach (Amazon.S3.Model.S3Object obj in response.S3Objects)
    {
      totalFiles++;

      Console.WriteLine($"Key: {obj.Key}");
      Console.WriteLine($"  Size: {FormatBytes(obj.Size ?? 0)}");
      Console.WriteLine($"  Last Modified: {obj.LastModified}");
      Console.WriteLine($"  Storage Class: {obj.StorageClass}");
      Console.WriteLine();
    }

    request.ContinuationToken = response.NextContinuationToken;
*/
    /*    
      } while (response.IsTruncated == true);

      htmlContent.Append("  </div>\n");

      htmlContent.AppendLine("<div style=\"width:100%; text-align: center\">");
      htmlContent.AppendLine($"Total files: {totalFiles}<BR/>");
      htmlContent.AppendLine($"Total skipped: {skippedFiles}<BR/>");
      htmlContent.AppendLine($"Total scans: {scanfiles}<BR/>");
      foreach (var dCount in updates)
        htmlContent.AppendLine($"{dCount.Key} has {dCount.Value} updates<BR/>");
      htmlContent.AppendLine("</div>");

      htmlContent.Append("</body>\n");

      Directory.SetCurrentDirectory(Path.GetDirectoryName(Util.CurrentQueryPath));
      // Write the content to a file
      File.WriteAllText(@".\scannedImages.html", htmlContent.ToString(), Encoding.UTF8);

      //update end

      Console.WriteLine($"Total files: {totalFiles}");
      Console.WriteLine($"Total skipped: {skippedFiles}");
      Console.WriteLine($"Total scans: {scanfiles}");
      updates.Dump();
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
  }

  */

    // Now generate the HTML page using the artList
    var html = new StringBuilder();
    html.AppendLine(GetHtmlHeader("All Artworks - Keith Long Archive"));

    html.AppendLine(@"
    <div class='container'>
        <h1>All Artworks</h1>
        <p class='subtitle'><a href='index.html'>← Back to Home</a></p>");

    html.AppendLine("<div class='gallery' style='font-size: x-small;'>");
    foreach (var artlist in artList.artworks) // while (await reader.ReadAsync())
    {
      Artwork art = artlist.Value;
/*
      if (art.states.HasFlag(StatesType.jpgFound))
      {
        Console.WriteLine("Found jpg for artwork: " + art.humanId);
      }


*/
      html.AppendLine($@"<div class='gallery-item'>");
      if ((art.states & StatesType.NoImage) == 0) // if we have an image
      {
        html.AppendLine($@"  <a href='{art.jpgURL}' target='_blank' rel='noopener noreferrer'>
                   <img src='{art.jpgURL}' title='(click for full size)'/>
                    </a><br/>
                    <div class='desc'><a class='desc' href='{art.tifURL}'>[tif file]</a></div>");

        // Add thumbnail buttons for additional views
        var thumbnails = new List<(string label, int? id)>
        {
          ("Back", art.backId),
          ("Front", art.frontId),
          ("Paper", art.paperId),
          ("Polaroid", art.polaroidId)
        };

        var thumbButtons = thumbnails
          .Where(t => t.id.HasValue)
          .Select(t =>
          {
            var thumbUrl = string.Format(S3_ARTWORK_IMAGE_URL, t.id!.Value, "small");
            var largeUrl = string.Format(S3_ARTWORK_IMAGE_URL, t.id!.Value, "large");
            return $"<a href='{largeUrl}' target='_blank' rel='noopener noreferrer' class='thumb-button' title='{t.label} view'><img src='{thumbUrl}' width='36' height='36' /></a>";
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
        html.AppendLine($"<div class='desc'>Bucket name: {art.iFileName}<br/></div>");

      if (art.states.HasFlag(StatesType.jpgFound))
        html.AppendLine($"<span class='heavy-check-mark'>&#x2705;</span>");
      if (art.states.HasFlag(StatesType.tifFound))
        html.AppendLine($"<span class='heavy-check-mark'>&#x2705;</span>");

      html.AppendLine($"</div>  <!-- gallery item -->");
    }

    html.AppendLine(@"</div>");
    html.AppendLine(GetHtmlFooter());

    await File.WriteAllTextAsync(Path.Combine(_outputDirectory, "artworksplus.html"), html.ToString());
  }


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
            return $"<a href='{largeUrl}' target='_blank' rel='noopener noreferrer' class='thumb-button' title='{t.label} view'><img src='{thumbUrl}' width='36' height='36' /></a>";
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

.nav-button:hover {
    background: #2980b9;
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
  }
  div.gallery-item:hover
  {
    border: 1px solid #777;
  }
  div.gallery-item img
  {
    width: 100%;
    height: auto;
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
    border: 2px solid #ccc;
    border-radius: 4px;
    transition: border-color 0.2s;
  }

  .thumb-button:hover {
    border-color: #3498db;
  }

  .thumb-button img {
    display: block;
    width: 36px;
    height: 36px;
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
<p>
    This website is NOT open to the public.   It is in development.  All images and content are (c) Estate of Keith Long.
</p>
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
