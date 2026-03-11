
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

  const string artworkYearSQL = @"
            SELECT
                EXTRACT(YEAR FROM create_dt)::int as year,
                COUNT(*) as piece_count,
                MIN(create_dt) as start_date,
                MAX(create_dt) as end_date,
                COUNT(DISTINCT NULLIF(TRIM(series), '')) as series_count
            FROM artwork
            WHERE create_dt IS NOT NULL AND EXTRACT(YEAR FROM create_dt) > 1900
            GROUP BY EXTRACT(YEAR FROM create_dt)
            ORDER BY year ASC";

  const string sketchbookDetailSQL = @"
            SELECT
                sketchbook_number,
                COUNT(*) as page_count,
                MIN(sketch_dt) as start_date,
                MAX(sketch_dt) as end_date
            FROM sketch
            GROUP BY sketchbook_number
            ORDER BY sketchbook_number ASC";


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
    await sketchReader.DisposeAsync();
    await sketchCmd.DisposeAsync();

    // Artwork year breakdown
    var artworkYears = new List<(int Year, int Count, string Start, string End, int SeriesCount)>();
    await using var yearCmd = new NpgsqlCommand(artworkYearSQL, connection);
    await using var yearReader = await yearCmd.ExecuteReaderAsync();
    while (await yearReader.ReadAsync())
    {
      artworkYears.Add((
        yearReader.GetInt32(0),
        yearReader.GetInt32(1),
        yearReader.IsDBNull(2) ? "" : yearReader.GetDateTime(2).ToString("MMM d, yyyy"),
        yearReader.IsDBNull(3) ? "" : yearReader.GetDateTime(3).ToString("MMM d, yyyy"),
        yearReader.IsDBNull(4) ? 0 : yearReader.GetInt32(4)
      ));
    }
    await yearReader.DisposeAsync();
    await yearCmd.DisposeAsync();

    // Sketchbook breakdown
    var sketchbookDetails = new List<(int Number, int Pages, string Start, string End)>();
    await using var sbDetailCmd = new NpgsqlCommand(sketchbookDetailSQL, connection);
    await using var sbDetailReader = await sbDetailCmd.ExecuteReaderAsync();
    while (await sbDetailReader.ReadAsync())
    {
      sketchbookDetails.Add((
        sbDetailReader.IsDBNull(0) ? 0 : sbDetailReader.GetInt32(0),
        sbDetailReader.GetInt32(1),
        sbDetailReader.IsDBNull(2) ? "" : sbDetailReader.GetDateTime(2).ToString("MMM d, yyyy"),
        sbDetailReader.IsDBNull(3) ? "" : sbDetailReader.GetDateTime(3).ToString("MMM d, yyyy")
      ));
    }

    // Build artwork year detail rows
    var artworkYearRows = new StringBuilder();
    foreach (var row in artworkYears)
    {
      artworkYearRows.AppendLine($@"
                <tr>
                    <td>{row.Year}</td>
                    <td>{row.Count}</td>
                    <td>{row.Start}</td>
                    <td>{row.End}</td>
                    <td>{row.SeriesCount}</td>
                </tr>");
    }

    // Build sketchbook detail rows
    var sketchbookRows = new StringBuilder();
    foreach (var row in sketchbookDetails)
    {
      sketchbookRows.AppendLine($@"
                <tr>
                    <td><a href='sketchbook{row.Number}.html'>Sketchbook {row.Number}</a></td>
                    <td>{row.Pages}</td>
                    <td>{row.Start}</td>
                    <td>{row.End}</td>
                </tr>");
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
        <details class='stats-details'>
            <summary>Details</summary>
            <table class='stats-table'>
                <thead>
                    <tr>
                        <th>Year</th>
                        <th>Pieces</th>
                        <th>Start Date</th>
                        <th>End Date</th>
                        <th>Named Series</th>
                    </tr>
                </thead>
                <tbody>" + artworkYearRows + @"</tbody>
            </table>
        </details>

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
        <details class='stats-details'>
            <summary>Details</summary>
            <table class='stats-table'>
                <thead>
                    <tr>
                        <th>Sketchbook</th>
                        <th>Pages</th>
                        <th>Start Date</th>
                        <th>End Date</th>
                    </tr>
                </thead>
                <tbody>" + sketchbookRows + @"</tbody>
            </table>
        </details>

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
