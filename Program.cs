using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using Npgsql;
using ArtWorkHTML;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;

class Program
{
    static void PrintUsage()
    {
        Console.WriteLine("Usage: dotnet run [-- <command>] [--dbsketchonly]");
        Console.WriteLine();
        Console.WriteLine("Generates the static HTML gallery from PostgreSQL data + S3 images.");
        Console.WriteLine("With no command, generates every page into Output:Directory.");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  gen-static              generate only static / non-data pages (no DB needed)");
        Console.WriteLine("  test-airtable           sanity-check the Airtable API connection and exit");
        Console.WriteLine("  test-db                 sanity-check the PostgreSQL connection and exit");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --dbsketchonly          restrict sketchbook pages to DB rows (skip extras from S3)");
        Console.WriteLine("  -h, --help, -?, /?, ?   show this help and exit");
        Console.WriteLine();
        Console.WriteLine("Configuration (appsettings.json):");
        Console.WriteLine("  Airtable:ApiKey/BaseId/TableName  used only by test-airtable");
        Console.WriteLine("  PostgreSQL:Host/Database/Port/SecretArn  DB connection");
        Console.WriteLine("  Output:Directory                  output folder (default: artwork_html)");
    }

    static async Task Main(string[] args)
    {
        if (args.Any(a => a is "-h" or "--help" or "-?" or "/?" or "?"))
        {
            PrintUsage();
            return;
        }
        foreach (var a in args)
        {
            if ((a.StartsWith("-") || a.StartsWith("/")) && a is not "--dbsketchonly")
            {
                Console.WriteLine($"Unknown option: {a}");
                Console.WriteLine();
                PrintUsage();
                Environment.ExitCode = 1;
                return;
            }
        }

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .Build();

        var airtableApiKey = configuration["Airtable:ApiKey"];
        var airtableBaseId = configuration["Airtable:BaseId"];
        var airtableTableName = configuration["Airtable:TableName"];
        var outputDirectory = configuration["Output:Directory"] ?? "artwork_html";
        bool staticOnly = false;

        // Get PostgreSQL credentials from AWS Secrets Manager
        var secretArn = configuration["PostgreSQL:SecretArn"];
        if (string.IsNullOrEmpty(secretArn))
        {
            throw new Exception("PostgreSQL:SecretArn not configured in appsettings.json");
        }

        // Build connection string with retrieved credentials
        var host = configuration["PostgreSQL:Host"];
        var database = configuration["PostgreSQL:Database"];
        var port = configuration["PostgreSQL:Port"] ?? "5432";

        Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║       Keith Long Archive - Artwork HTML Generation         ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        // Check command line arguments
        if (args.Length > 0 && args[0] == "gen-static")
        {
            Console.WriteLine("Generating statistic page only... (no database connection required in generation)");
            staticOnly = true;
        }

        (string username, string password) dbCredentials=("","");

        if (staticOnly)
           Console.WriteLine("Static only, don't need credentials from AWS Secrets Manager...");
        else
        {
          Console.WriteLine("Retrieving database credentials from AWS Secrets Manager...");
          dbCredentials = await GetDatabaseCredentialsFromSecretsManager(secretArn);
          Console.WriteLine("✓ Database credentials retrieved successfully\n");
        }
        var postgresConnectionString = $"Host={host};Port={port};Database={database};Username={dbCredentials.username};Password={dbCredentials.password};SSL Mode=Require;Trust Server Certificate=true";


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

        if (args.Contains("--dbsketchonly"))
        {
            generator.DbSketchOnly = true;
            Console.WriteLine("Mode: --dbsketchonly (sketchbook pages limited to database entries only)");
        }

        Console.WriteLine("Generating HTML pages...");
        if (staticOnly)
          await generator.GenerateStaticPages();
        else
          await generator.GenerateAllPages();

        Console.WriteLine($"\n✓ HTML files generated successfully!");
        var indexPath = Path.Combine(fullOutputPath, "index.html");
        var indexUrl = "file:///" + indexPath.Replace('\\', '/');
        Console.WriteLine($"✓ Open: \e]8;;{indexUrl}\e\\{indexPath}\e]8;;\e\\");
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
