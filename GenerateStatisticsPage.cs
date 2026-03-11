
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
  const string statsSQL = @"
            SELECT
                COUNT(*) as total_artworks,
                COUNT(DISTINCT series) as total_series,
                COUNT(DISTINCT location) as total_locations,
                MIN(create_dt) as earliest_date,
                MAX(create_dt) as latest_date
            FROM artwork
            WHERE create_dt IS NOT NULL and EXTRACT(YEAR FROM create_dt) > 1900";

  const string sketchStatsSQL = @"
            SELECT
                COUNT(DISTINCT sketchbook_number) as total_sketchbooks,
                COUNT(*) as total_pages,
                MIN(sketch_dt) as earliest_date,
                MAX(sketch_dt) as latest_date
            FROM sketch
            WHERE sketch_dt IS NOT NULL and EXTRACT(YEAR FROM sketch_dt) > 1900";


  private async Task GenerateStatisticsPage()
  {
    await using var connection = new NpgsqlConnection(_connectionString);
    await connection.OpenAsync();

    await using var cmd = new NpgsqlCommand(statsSQL, connection);
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
    await reader.DisposeAsync();
    await cmd.DisposeAsync();

    int totalSketchbooks = 0, totalSketchPages = 0;
    string? earliestSketchDate = null, latestSketchDate = null;
    await using var sketchCmd = new NpgsqlCommand(sketchStatsSQL, connection);
    await using var sketchReader = await sketchCmd.ExecuteReaderAsync();
    if (await sketchReader.ReadAsync())
    {
      totalSketchbooks = sketchReader.IsDBNull(0) ? 0 : sketchReader.GetInt32(0);
      totalSketchPages = sketchReader.IsDBNull(1) ? 0 : sketchReader.GetInt32(1);
      earliestSketchDate = sketchReader.IsDBNull(2) ? null : sketchReader.GetDateTime(2).ToString("yyyy");
      latestSketchDate = sketchReader.IsDBNull(3) ? null : sketchReader.GetDateTime(3).ToString("yyyy");
    }

    var html = new StringBuilder();
    html.AppendLine(GetHtmlHeader("Archive Statistics - Keith Long Archive"));
    html.AppendLine(@"
    <div class='container'>
        <h1>Archive Statistics</h1>
        <p class='subtitle'><a href='index.html'>← Back to Home</a></p>

        <h2>Artworks</h2>
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

        <h2>Sketchbooks</h2>
        <div class='stats-grid'>
            <div class='stat-card'>
                <div class='stat-number'>" + totalSketchbooks + @"</div>
                <div class='stat-label'>Sketchbooks</div>
            </div>
            <div class='stat-card'>
                <div class='stat-number'>" + totalSketchPages + @"</div>
                <div class='stat-label'>Sketchbook Pages</div>
            </div>
            <div class='stat-card'>
                <div class='stat-number'>" + earliestSketchDate + " - " + latestSketchDate + @"</div>
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

}