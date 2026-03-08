
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
            WHERE create_dt IS NOT NULL and EXTRACT(YEAR FROM create_dt) > 1900";

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

}