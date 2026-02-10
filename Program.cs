using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using Npgsql;
using ArtWorkHTML;

class Program
{
    static async Task Main(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .Build();

        var airtableApiKey = configuration["Airtable:ApiKey"];
        var airtableBaseId = configuration["Airtable:BaseId"];
        var airtableTableName = configuration["Airtable:TableName"];
        var postgresConnectionString = configuration["PostgreSQL:ConnectionString"];
        var outputDirectory = configuration["Output:Directory"] ?? "artwork_html";

        Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║       Keith Long Archive - Artwork HTML Generation         ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        // Check command line arguments
        if (args.Length > 0 && args[0] == "test-airtable")
        {
            Console.WriteLine("Testing Airtable connection...");
            await TestAirtableConnection(airtableApiKey!, airtableBaseId!, airtableTableName!);
            return;
        }

        if (args.Length > 0 && args[0] == "test-db")
        {
            Console.WriteLine("Testing PostgreSQL connection...");
            await TestDatabaseConnection(postgresConnectionString!);
            return;
        }

        // Default: Generate HTML files
        var fullOutputPath = Path.Combine(Directory.GetCurrentDirectory(), outputDirectory);
        Directory.CreateDirectory(fullOutputPath);
        Console.WriteLine($"Output directory: {fullOutputPath}\n");

        var generator = new ArtworkHTML(postgresConnectionString!, fullOutputPath);

        Console.WriteLine("Generating HTML pages...");
        await generator.GenerateAllPages();

        Console.WriteLine($"\n✓ HTML files generated successfully!");
        Console.WriteLine($"✓ Open: {Path.Combine(fullOutputPath, "index.html")}");
    }

    static async Task TestAirtableConnection(string apiKey, string baseId, string tableName)
    {
        try
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

            var url = $"https://api.airtable.com/v0/{baseId}/{Uri.EscapeDataString(tableName)}?maxRecords=1";
            var response = await httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var json = JObject.Parse(content);
            var records = json["records"] as JArray;

            Console.WriteLine($"✓ Connected to Airtable successfully!");
            Console.WriteLine($"✓ Table: {tableName}");
            Console.WriteLine($"✓ Sample records retrieved: {records?.Count ?? 0}");

            if (records?.Count > 0)
            {
                Console.WriteLine("\nFirst record fields:");
                var firstRecord = records[0] as JObject;
                var fields = firstRecord?["fields"] as JObject;
                if (fields != null)
                {
                    foreach (var field in fields.Properties())
                    {
                        Console.WriteLine($"  - {field.Name}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error connecting to Airtable: {ex.Message}");
        }
    }

    static async Task TestDatabaseConnection(string connectionString)
    {
        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            var sql = "SELECT COUNT(*) FROM artwork";
            await using var cmd = new NpgsqlCommand(sql, connection);
            var count = await cmd.ExecuteScalarAsync();

            Console.WriteLine($"✓ Connected to PostgreSQL successfully!");
            Console.WriteLine($"✓ Total artworks in database: {count}");

            // Check table schema
            var schemaSql = @"
                SELECT column_name, data_type
                FROM information_schema.columns
                WHERE table_name = 'artwork'
                ORDER BY ordinal_position
                LIMIT 10";

            await using var schemaCmd = new NpgsqlCommand(schemaSql, connection);
            await using var reader = await schemaCmd.ExecuteReaderAsync();

            Console.WriteLine("\nFirst 10 columns in artwork table:");
            while (await reader.ReadAsync())
            {
                var columnName = reader.GetString(0);
                var dataType = reader.GetString(1);
                Console.WriteLine($"  - {columnName} ({dataType})");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error connecting to PostgreSQL: {ex.Message}");
        }
    }
}
