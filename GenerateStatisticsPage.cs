
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
            WHERE sketch_dt IS NOT NULL and EXTRACT(YEAR FROM sketch_dt) > 1900
              AND hide IS NOT TRUE";

  const string artworkYearSQL = @"
            SELECT
                EXTRACT(YEAR FROM create_dt)::int as year,
                COUNT(*) as piece_count,
                MIN(create_dt) as start_date,
                MAX(create_dt) as end_date,
                COUNT(DISTINCT NULLIF(TRIM(series), '')) as series_count,
                COUNT(*) FILTER (WHERE sold IS NOT NULL) as sold_count
            FROM artwork
            WHERE create_dt IS NOT NULL AND EXTRACT(YEAR FROM create_dt) >= 1900
            GROUP BY EXTRACT(YEAR FROM create_dt)
            ORDER BY CASE WHEN EXTRACT(YEAR FROM create_dt) = 1900 THEN 9999 ELSE EXTRACT(YEAR FROM create_dt) END ASC";

  const string artworkSeriesSQL = @"
           SELECT
              COALESCE(NULLIF(TRIM(series), ''), '(no series)') as series_name,
              COUNT(*) as piece_count,
              MIN(create_dt) FILTER (WHERE EXTRACT(YEAR FROM create_dt) > 1900) as start_date,
              MAX(create_dt) as end_date,
              COUNT(*) FILTER (WHERE sold IS NOT NULL) as sold_count
            FROM artwork
            GROUP BY COALESCE(NULLIF(TRIM(series), ''), '(no series)')
            ORDER BY CASE WHEN COALESCE(NULLIF(TRIM(series), ''), '(no series)') = '(no series)' THEN 9999 ELSE 0 END ASC, series_name ASC";

// does not count date of 1900 TODO
  const string artworkLocationSQL = @"
            SELECT
                COALESCE(NULLIF(TRIM(location), ''), '(no location)') as location_name,
                COUNT(*) as piece_count,
                MIN(create_dt) FILTER (WHERE EXTRACT(YEAR FROM create_dt) > 1900) as start_date,
                MAX(create_dt) as end_date,
                COUNT(*) FILTER (WHERE sold IS NOT NULL) as sold_count
            FROM artwork
            GROUP BY COALESCE(NULLIF(TRIM(location), ''), '(no location)')
            ORDER BY CASE WHEN COALESCE(NULLIF(TRIM(location), ''), '(no location)') = '(no location)' THEN 9999 ELSE 0 END ASC, location_name ASC";

  const string sketchbookDetailSQL = @"
            SELECT
                sketchbook_number,
                COUNT(*) as page_count,
                MIN(sketch_dt) as start_date,
                MAX(sketch_dt) as end_date
            FROM sketch
            WHERE hide IS NOT TRUE
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
    var artworkYears = new List<(int Year, int Count, string Start, string End, int SeriesCount, int Sold)>();
    await using var yearCmd = new NpgsqlCommand(artworkYearSQL, connection);
    await using var yearReader = await yearCmd.ExecuteReaderAsync();
    while (await yearReader.ReadAsync())
    {
      artworkYears.Add((
        yearReader.GetInt32(0),
        yearReader.GetInt32(1),
        yearReader.IsDBNull(2) ? "" : yearReader.GetDateTime(2).ToString("MMM d, yyyy"),
        yearReader.IsDBNull(3) ? "" : yearReader.GetDateTime(3).ToString("MMM d, yyyy"),
        yearReader.IsDBNull(4) ? 0 : yearReader.GetInt32(4),
        yearReader.IsDBNull(5) ? 0 : yearReader.GetInt32(5)
      ));
    }
    await yearReader.DisposeAsync();
    await yearCmd.DisposeAsync();

    // Artwork series breakdown
    var artworkSeries = new List<(string Name, int Count, string Start, string End, int Sold)>();
    await using var seriesCmd = new NpgsqlCommand(artworkSeriesSQL, connection);
    await using var seriesReader = await seriesCmd.ExecuteReaderAsync();
    while (await seriesReader.ReadAsync())
    {
      artworkSeries.Add((
        seriesReader.IsDBNull(0) ? "" : seriesReader.GetString(0),
        seriesReader.GetInt32(1),
        seriesReader.IsDBNull(2) ? "" : seriesReader.GetDateTime(2).ToString("MMM d, yyyy"),
        seriesReader.IsDBNull(3) ? "" : seriesReader.GetDateTime(3).ToString("MMM d, yyyy"),
        seriesReader.IsDBNull(4) ? 0 : seriesReader.GetInt32(4)
      ));
    }
    await seriesReader.DisposeAsync();
    await seriesCmd.DisposeAsync();

    // Artwork location breakdown
    var artworkLocations = new List<(string Name, int Count, string Start, string End, int Sold)>();
    await using var locationCmd = new NpgsqlCommand(artworkLocationSQL, connection);
    await using var locationReader = await locationCmd.ExecuteReaderAsync();
    while (await locationReader.ReadAsync())
    {
      artworkLocations.Add((
        locationReader.IsDBNull(0) ? "" : locationReader.GetString(0),
        locationReader.GetInt32(1),
        locationReader.IsDBNull(2) ? "" : locationReader.GetDateTime(2).ToString("MMM d, yyyy"),
        locationReader.IsDBNull(3) ? "" : locationReader.GetDateTime(3).ToString("MMM d, yyyy"),
        locationReader.IsDBNull(4) ? 0 : locationReader.GetInt32(4)
      ));
    }
    await locationReader.DisposeAsync();
    await locationCmd.DisposeAsync();

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
      bool clearDates = row.Year == 1900; // Assuming 1900 is used as a placeholder for unknown years
      string yearDisplay = clearDates ? "Unknown" : row.Year.ToString();
      string yearSeriesDisplay = row.SeriesCount == 0 ? "" : row.SeriesCount.ToString();
      string yearSoldDisplay = row.Sold == 0 ? "" : row.Sold.ToString();
      artworkYearRows.AppendLine($@"
                <tr>
                    <td>{yearDisplay}</td>
                    <td>{row.Count}</td>
                    <td>{(clearDates ? "" : row.Start)}</td>
                    <td>{(clearDates ? "" : row.End)}</td>
                    <td>{yearSeriesDisplay}</td>
                    <td>{yearSoldDisplay}</td>
                </tr>");
    }

    // Build artwork series detail rows
    var artworkSeriesRows = new StringBuilder();
    foreach (var row in artworkSeries)
    {
      artworkSeriesRows.AppendLine($@"
                <tr>
                    <td>{row.Name}</td>
                    <td>{row.Count}</td>
                    <td>{row.Start}</td>
                    <td>{row.End}</td>
                    <td>{(row.Sold == 0 ? "" : row.Sold.ToString())}</td>
                </tr>");
    }

    // Build artwork location detail rows
    var artworkLocationRows = new StringBuilder();
    foreach (var row in artworkLocations)
    {
      artworkLocationRows.AppendLine($@"
                <tr>
                    <td>{row.Name}</td>
                    <td>{row.Count}</td>
                    <td>{row.Start}</td>
                    <td>{row.End}</td>
                    <td>{(row.Sold == 0 ? "" : row.Sold.ToString())}</td>
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
    <script>
    function showStatsView(btn, viewId) {
        var container = btn.closest('.stats-views');
        container.querySelectorAll('.stats-view').forEach(function(v) { v.style.display = 'none'; });
        container.querySelectorAll('.stats-tab').forEach(function(t) { t.classList.remove('active'); });
        document.getElementById(viewId).style.display = '';
        btn.classList.add('active');
    }
    </script>

    <div class='container'>
        <h1>Archive Statistics</h1>
        <p class='subtitle'><a href='index.html'>← Back to Home</a></p>

        <h2>Artworks</h2>
        <a href='artworksplus.html' class='nav-button nav-button-sm'>Browse All Artworks</a>
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
            <div class='stats-views'>
                <div class='stats-tab-bar'>
                    <button class='stats-tab active' onclick='showStatsView(this, ""aw-year"")'>By Year</button>
                    <button class='stats-tab' onclick='showStatsView(this, ""aw-series"")'>By Series</button>
                    <button class='stats-tab' onclick='showStatsView(this, ""aw-location"")'>By Location</button>
                </div>
                <div id='aw-year' class='stats-view'>
                    <table class='stats-table'>
                        <thead><tr>
                            <th>Year</th><th>Pieces</th><th>Start Date</th><th>End Date</th><th>Named Series</th><th>Sold</th>
                        </tr></thead>
                        <tbody>" + artworkYearRows + @"</tbody>
                    </table>
                </div>
                <div id='aw-series' class='stats-view' style='display:none'>
                    <table class='stats-table'>
                        <thead><tr>
                            <th>Series</th><th>Count</th><th>Start Date</th><th>End Date</th><th>Sold</th>
                        </tr></thead>
                        <tbody>" + artworkSeriesRows + @"</tbody>
                    </table>
                </div>
                <div id='aw-location' class='stats-view' style='display:none'>
                    <table class='stats-table'>
                        <thead><tr>
                            <th>Location</th><th>Count</th><th>Start Date</th><th>End Date</th><th>Sold</th>
                        </tr></thead>
                        <tbody>" + artworkLocationRows + @"</tbody>
                    </table>
                </div>
            </div>
        </details>

        <h2>Sketchbooks</h2>
        <a href='sketchbook1.html' class='nav-button nav-button-sm'>Browse All Sketchbooks</a>
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

    </div>");
    html.AppendLine(GetHtmlFooter());

    await File.WriteAllTextAsync(Path.Combine(_outputDirectory, "statistics.html"), html.ToString());
  }

}
