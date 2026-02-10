# ArtWorkHTML Generator

A .NET console application that generates HTML pages from artwork data stored in PostgreSQL database and Airtable.

## Features

- Connects to both Airtable API and PostgreSQL database
- Generates multiple HTML pages:
  - `index.html` - Main landing page with statistics
  - `artworks.html` - Gallery view of all artworks with images
  - `series.html` - Artworks grouped by series
  - `locations.html` - Artworks grouped by location
  - `style.css` - Responsive stylesheet

## Prerequisites

- .NET 10.0 SDK or later
- Access to PostgreSQL database with artwork table
- Airtable API credentials (optional, for testing)

## Configuration

Edit `appsettings.json` to configure:

```json
{
  "Airtable": {
    "ApiKey": "your-airtable-api-key",
    "BaseId": "your-base-id",
    "TableName": "ARTWORK"
  },
  "PostgreSQL": {
    "ConnectionString": "your-postgresql-connection-string"
  },
  "Output": {
    "Directory": "artwork_html"
  }
}
```

## Usage

### Generate HTML files (default)
```bash
dotnet run
```

### Test Airtable connection
```bash
dotnet run test-airtable
```

### Test PostgreSQL connection
```bash
dotnet run test-db
```

## Output

HTML files are generated in the `artwork_html` directory (configurable via appsettings.json).

Open `artwork_html/index.html` in a web browser to view the generated website.

## Database Schema

The application expects an `artwork` table with the following columns:
- `id_field` - Primary key
- `iFileName` - Image filename
- `title` - Artwork title
- `series` - Series name
- `create_dt` - Creation date
- `medium` - Medium used
- `dimensions` - Artwork dimensions
- `FOLDED_DIMENSIONS` - Folded dimensions (optional)
- `location` - Current location
- `notes` - Additional notes
- `human_readable_id` - Human-readable identifier
- `artwork_image_id` - Image identifier

## Project Structure

```
ArtWorkHTML/
├── Program.cs           - Main entry point
├── ArtworkHTML.cs       - HTML generation logic
├── appsettings.json     - Configuration
├── ArtWorkHTML.csproj   - Project file
└── README.md           - This file
```

## Dependencies

- Microsoft.Extensions.Configuration (10.0.2)
- Newtonsoft.Json (13.0.4)
- Npgsql (10.0.1) - PostgreSQL connector
