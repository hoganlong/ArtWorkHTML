using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using Npgsql;
using ArtWorkHTML;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;

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
        var outputDirectory = configuration["Output:Directory"] ?? "artwork_html";

        // Get PostgreSQL credentials from AWS Secrets Manager
        var secretArn = configuration["PostgreSQL:SecretArn"];
        if (string.IsNullOrEmpty(secretArn))
        {
            throw new Exception("PostgreSQL:SecretArn not configured in appsettings.json");
        }

        Console.WriteLine("Retrieving database credentials from AWS Secrets Manager...");
        var dbCredentials = await GetDatabaseCredentialsFromSecretsManager(secretArn);

        // Build connection string with retrieved credentials
        var host = configuration["PostgreSQL:Host"];
        var database = configuration["PostgreSQL:Database"];
        var port = configuration["PostgreSQL:Port"] ?? "5432";

        var postgresConnectionString = $"Host={host};Port={port};Database={database};Username={dbCredentials.username};Password={dbCredentials.password};SSL Mode=Require;Trust Server Certificate=true";
        Console.WriteLine("✓ Database credentials retrieved successfully\n");

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

    static async Task<(string username, string password)> GetDatabaseCredentialsFromSecretsManager(string secretArn)
    {
        try
        {
            var region = Amazon.RegionEndpoint.USEast1;
            var client = new AmazonSecretsManagerClient(region);

            var request = new GetSecretValueRequest
            {
                SecretId = secretArn
            };

            var response = await client.GetSecretValueAsync(request);
            var secretString = response.SecretString;

            // Parse the JSON secret
            var secret = JObject.Parse(secretString);
            var username = secret["username"]?.ToString() ?? throw new Exception("Username not found in secret");
            var password = secret["password"]?.ToString() ?? throw new Exception("Password not found in secret");

            return (username, password);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error retrieving secret from AWS Secrets Manager: {ex.Message}");
            throw;
        }
    }
}
