# ArtWorkHTML Generator

A .NET 10 console application that generates a static HTML website from artwork data stored in a PostgreSQL database and AWS S3.

## Features

- Connects to PostgreSQL for artwork metadata
- Reads images from AWS S3 bucket (`keithlong-art-photos`, us-east-1)
- Retrieves database credentials securely from AWS Secrets Manager
- Generates multiple HTML pages:
  - `index.html` — Landing/navigation page with centered content layout
  - `statistics.html` — Stats with collapsible details: Artworks section (tabbed By Year / By Series / By Location / By Type, each with browse buttons) and Sketchbooks section (per-book table with links)
  - `artwork.html` — Main gallery with thumbnails, type filter, hover effects, tag-driven visibility
  - `polaroids.html` — Polaroid scan gallery
  - `sketchbooks.html` — Sketchbook index with intro text and a button for each sketchbook
  - `sketchbooks/sketchbook1.html`, `sketchbooks/sketchbook2.html`, ... — One page per sketchbook (generated dynamically into `sketchbooks/` subfolder)
  - `hide.html` — Index of hidden sketchbook pages (generated if any sketch rows have `hide = true`, or if bucket-only sketch files exist)
  - `hide/sketchbook1.html`, `hide/sketchbook2.html`, ... — One page per sketchbook containing pages with `hide = true` **plus** any bucket-only sketch files not found in the DB (generated dynamically into `hide/` subfolder)
  - `copyright.html`, `howisitmade.html`, `credits.html`, `help.html`, `feedback.html`, `opensource.html` — Info pages (linked from footer)
  - `style.css` — Generated stylesheet

### Error Reporting
- At end of each run, prints a summary of error counts to console and appends it as an HTML comment at the end of `artwork.html`
- Errors tracked: `Missing front photo`, `Missing back photo` (type W only), `Bucket image not found`, `Was found on server and not DB`, `Duplicate humanId`

### Gallery Features
- **TAGS system** — Gallery items hidden by default; shown when their tags match active tags (see URL Parameters below)
- **Type filter** — Filter artworks by type (All checkbox + per-type checkboxes, built dynamically from `<my-tags>` first tag)
- **Series button** — Each artwork with a series shows a small ꜱ button; clicking it filters to show only that series
- **Thumbnail preview** — Hover over a thumbnail button to see a larger preview popup (injected lazily on first hover — not pre-loaded)
- **Adaptive thumbnail sizing** — Wide thumbnails expand horizontally (up to 220px); tall/portrait thumbnails swap to the large S3 image and expand vertically (up to 120px) to avoid blur from upscaling
- **Image zoom** — Hover over a gallery image to zoom in (2x, anchored to bottom)
- **Keyboard shortcuts** — `z` image zoom toggle, `p` thumbnail preview toggle, `t` scroll to top
- **Lazy image loading** — All gallery images use `loading="lazy"`; hover preview images are injected into the DOM on first mouseenter rather than pre-loaded at page load

### URL Parameters (gallery pages)
| Parameter | Example | Effect |
|-----------|---------|--------|
| `show=x` | `?show=all` | Filter by tag; `all` shows everything |
| `tagtitle=x` | `?tagtitle=1982` | Filter by tag AND show large title banner + update page h1 |
| `tag=x` | `?tag=Drawing` | Filter by tag |
| `key=true` | `?Drawing=true` | Filter by tag (key name used as tag) |
| `#anchor` | `#chapeau` | Filter by tag |
| `back=url` | `&back=statistics.html` | Override back-link href |
| `backlabel=text` | `&backlabel=Return+to+Statistics` | Override back-link text |

Multiple tags: comma-separated (`?show=1982,Drawing`). Special tag `ALL` shows everything.
Tags are case-insensitive. Tags cookie `TAGS=a,b,c` also supported.

### Tag Data Per Gallery Item (artwork.html)
Each artwork gallery item contains:
- `<my-tags>` — **Visible** comma-separated tags: `{TypeTag},{Year},{HumanId}` (e.g. `Drawing,1982,KLA-042`)
- `<my-hidden-tags>` — **Hidden** comma-separated tags: `{SeriesTag},{LocationTag}` (spaces→`-`, commas stripped via `MakeTag()`)

**Known issue**: Type filter checkboxes assume the type tag is always the first value in `<my-tags>`. If tag order changes, checkboxes break. Future fix: use a dedicated element or data attribute for type.

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

## Full Pipeline / Deployment

The full build-and-deploy pipeline is automated by `build-and-deploy.ps1` in the parent directory (`D:\Projects\claudetest\`).

### Run the full pipeline
```powershell
cd D:\Projects\claudetest
.\build-and-deploy.ps1
```

### Skip to a specific step
```powershell
.\build-and-deploy.ps1 -StartStep 4   # start from HTML generation
.\build-and-deploy.ps1 -StartStep 5   # start from S3 sync
```

### Pipeline steps
| # | Step | Project/Command |
|---|------|-----------------|
| 1 | Download new images from Airtable | `AirtableImageDownloader` — `dotnet run` |
| 2 | Upload new local images to S3 | `checks3vslocal` — `dotnet run` |
| 3 | ETL: Airtable → PostgreSQL | `AirtableToPostgres` — `dotnet run` |
| 4 | Generate static HTML site | `ArtWorkHTML` — `dotnet run` |
| 5 | Sync HTML to website S3 bucket | `aws s3 sync artwork_html/ s3://archive.keithlong.com/ --delete` |
| 6 | Invalidate CloudFront cache | `aws cloudfront create-invalidation ...` |

Skipped steps are printed in gray so you can see what was bypassed. Any step failure aborts the pipeline.

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
- `human_readable_id` — Human-readable identifier. Format: `KL_{year}_{typeCode}_{number}`
  where year is 4-digit creation year, typeCode is the single-letter artwork type code
  (W/D/S/C/J/P/B/N), and number is zero-padded to 4 digits. Example: `KL_1982_D_0042`.
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
- `hide` — boolean; pages with `hide = true` are excluded from `sketchbooks/` pages and instead appear only in `hide/` pages. Bucket-only sketch files (in S3 but not in the DB) also appear in `hide/` pages regardless of this flag.

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
├── ArtworkHTML.cs              — Core helpers: GetHtmlHeader/Footer, GetTagsScript, GetTypeTag,
│                                 MakeTag, TypeDescriptions, TypeTags (partial class)
├── GenerateIndexPage.cs        — Landing page generation (partial class)
├── GenerateStylesheet.cs       — CSS stylesheet generation (partial class)
├── GenerateArtworkPages.cs     — Gallery page generation (partial class)
├── GenerateStatisticsPage.cs   — Statistics page generation (partial class)
├── GenerateCopyrightPage.cs    — copyright.html (partial class)
├── GenerateHowIsMadePage.cs    — howisitmade.html (partial class)
├── GenerateCreditsPage.cs      — credits.html (partial class)
├── GenerateHelpPage.cs         — help.html (partial class)
├── GenerateFeedbackPage.cs     — feedback.html (partial class)
├── GenerateOpensourcePage.cs   — opensource.html (partial class)
├── ArtList.cs                  — ArtList collection + Artwork model + enums
├── appsettings.json            — Configuration
├── ArtWorkHTML.csproj          — Project file
└── README.md                   — This file
```

## Key Helper Methods (ArtworkHTML.cs)

| Method | Purpose |
|--------|---------|
| `GetHtmlHeader(title, pathPrefix)` | HTML `<head>` + site notice; pass `"../"` for subdir pages |
| `GetHtmlFooter(pathPrefix)` | Footer nav + closing tags |
| `GetTagsScript()` | JS IIFE injected into gallery pages; handles all tag sources, back-nav, tagtitle banner, page title, `window._tagState`, `window._filterToTag()` |
| `GetTypeDescription(typeCode)` | Human-readable type name from `TypeDescriptions` dict |
| `GetTypeTag(typeCode)` | Hyphenated tag name from `TypeTags` dict (e.g. `"Wall-Hanging-Sculpture"`) |
| `MakeTag(s)` | Sanitizes a string for use as a tag: spaces→`-`, strips `,'"`, trims `-` |

## Dependencies

- `Microsoft.Extensions.Configuration` (10.0.2)
- `Newtonsoft.Json` (13.0.4)
- `Npgsql` (10.0.1) — PostgreSQL connector
- `AWSSDK.S3` — S3 image listing
- `AWSSDK.SecretsManager` — Database credential retrieval
