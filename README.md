# ArtWorkHTML Generator

A .NET 10 console application that generates a static HTML website from artwork data stored in a PostgreSQL database and AWS S3.

## Features

- Connects to PostgreSQL for artwork metadata
- Reads images from AWS S3 bucket (`keithlong-art-photos`, us-east-1)
- Retrieves database credentials securely from AWS Secrets Manager
- Generates multiple HTML pages:
  - `index.html` — Landing/navigation page with centered content layout
  - `statistics.html` — Stats with collapsible details: Artworks section (tabbed By Year / By Series / By Location, each with Sold count) and Sketchbooks section (per-book table with links)
  - `artworksplus.html` — Main gallery with thumbnails, type filter, hover effects
  - `polaroids.html` — Polaroid scan gallery
  - `sketchbook1.html`, `sketchbook2.html`, ... — One page per sketchbook (generated dynamically)
  - `copyright.html`, `howisitmade.html`, `credits.html`, `help.html`, `feedback.html` — Info pages (linked from footer)
  - `style.css` — Generated stylesheet

### Gallery Features
- **Type filter** — Filter artworks by type code (All checkbox + per-type checkboxes, built dynamically)
- **Thumbnail preview** — Hover over a thumbnail button to see a larger preview popup
- **Image zoom** — Hover over a gallery image to zoom in (2x, anchored to bottom)
- **Keyboard shortcuts** — `z` image zoom toggle, `p` thumbnail preview toggle, `t` scroll to top

## Prerequisites

- .NET 10.0 SDK or later
- AWS credentials configured (for Secrets Manager + S3 access)
- Access to PostgreSQL database

## Configuration

Edit `appsettings.json`:

```json
{
  "Airtable": {
    "ApiKey": "your-airtable-api-key",
    "BaseId": "your-base-id",
    "TableName": "ARTWORK"
  },
  "PostgreSQL": {
    "Host": "your-db-host",
    "Database": "your-database-name",
    "Port": "5432",
    "SecretArn": "arn:aws:secretsmanager:us-east-1:..."
  },
  "Output": {
    "Directory": "artwork_html"
  }
}
```

PostgreSQL username and password are fetched at runtime from AWS Secrets Manager using `SecretArn`. The secret must contain `username` and `password` fields.

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

HTML files are generated in the `artwork_html` directory (configurable via `appsettings.json`).

Open `artwork_html/index.html` in a web browser to view the generated website.

## Database Schema

### `artwork` table
- `id_field` — Primary key
- `airtable_id` — Airtable record ID
- `FileName` — Image filename (used to build S3 URL)
- `title` — Artwork title
- `series` — Series name
- `create_dt` — Creation date
- `medium` — Medium used
- `dimensions` — Artwork dimensions
- `FOLDED_DIMENSIONS` — Folded dimensions (optional)
- `location` — Current location
- `notes` — Additional notes
- `human_readable_id` — Human-readable identifier
- `artwork_image_id` — Legacy image identifier
- `type_id` — Foreign key to `artwork_type` (stored as JSON array)

### `artwork_image` table
- `id_field` — Primary key
- `artwork_id` — References `artwork.airtable_id`
- `view` — View type: Back, Front, Paper, Polaroid
- `url` — Direct URL (if set, overrides S3 attachment path)

### `artwork_type` table
- `airtable_id` — Matches `artwork.type_id`
- `code` — Single-letter type code (W, D, S, C, J, P, B, N)
- `description` — Display name

### `sketch` table
- `airtable_id`, `sketch_dt`, `description`, `sketch_loc`, `sketch_people`
- `sketch_medium`, `sketchbook_number`, `page_number`, `artwork_id`, `filename`, `pub_notes`

## S3 Bucket Structure

Bucket: `keithlong-art-photos` (us-east-1)

| Path | Contents |
|------|----------|
| `jpg/` | Main artwork JPGs |
| `atch/` | Attachment images (`artwork_{id}_{size}.jpg`) |
| `scans/` | Sketchbook TIF scans and polaroid TIF scans |
| `scans/jpg/` | Sketchbook JPGs (`KLA_*`) and polaroid JPGs |

## Project Structure

```
ArtWorkHTML/
├── Program.cs                  — Entry point, AWS Secrets Manager, test commands
├── ArtworkHTML.cs              — Core helpers, header/footer, GenerateAllPages (partial class)
├── GenerateIndexPage.cs        — Landing page generation (partial class)
├── GenerateStylesheet.cs       — CSS stylesheet generation (partial class)
├── GenerateArtworkPages.cs     — Gallery page generation (partial class)
├── GenerateStatisticsPage.cs   — Statistics page generation (partial class)
├── GenerateCopyrightPage.cs    — copyright.html (partial class)
├── GenerateHowIsMadePage.cs    — howisitmade.html (partial class)
├── GenerateCreditsPage.cs      — credits.html (partial class)
├── GenerateHelpPage.cs         — help.html (partial class)
├── GenerateFeedbackPage.cs     — feedback.html (partial class)
├── ArtList.cs                  — ArtList collection + Artwork model + enums
├── appsettings.json            — Configuration
├── ArtWorkHTML.csproj          — Project file
└── README.md                   — This file
```

## Dependencies

- `Microsoft.Extensions.Configuration` (10.0.2)
- `Newtonsoft.Json` (13.0.4)
- `Npgsql` (10.0.1) — PostgreSQL connector
- `AWSSDK.S3` — S3 image listing
- `AWSSDK.SecretsManager` — Database credential retrieval
