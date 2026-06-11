using System.Text;

namespace ArtWorkHTML;

public partial class ArtworkHTML
{
  private async Task GenerateStylesheet()
  {
    var css = @"
* {
    margin: 0;
    padding: 0;
    box-sizing: border-box;
}

body {
    font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Oxygen, Ubuntu, sans-serif;
    line-height: 1.6;
    color: #333;
    background: #f5f5f5;
}

ul {
  list-style-position: outside;
  padding-left: 20px;
  margin-left: 0px;
}

.container {
    max-width: 80%;
    margin: 0 auto;
    padding: 20px;
}

.oldcontainer {
    max-width: 1400px;
    margin: 0 auto;
    padding: 20px;
}

h1 {
    font-size: 2.5em;
    margin-bottom: 10px;
    color: #2c3e50;
}

.subtitle {
    color: #7f8c8d;
    margin-bottom: 30px;
}

.subtitle a {
    color: #3498db;
    text-decoration: none;
}

.subtitle a:hover {
    text-decoration: underline;
}

.landing-header {
    max-width: 1000px;
    margin: 0 auto;
}

.landing-content {
    font-size: 1.0em;
    line-height: 1.2;
    max-width: 1000px;
    margin: 0 auto;
}

.landing-content p {
    margin-bottom: 1em;
}

.stats-grid {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
    gap: 20px;
    margin: 40px 0;
}

.stat-card {
    background: white;
    padding: 30px;
    border-radius: 8px;
    box-shadow: 0 2px 4px rgba(0,0,0,0.1);
    text-align: center;
}

.stat-number {
    font-size: 2.5em;
    font-weight: bold;
    color: #3498db;
    margin-bottom: 10px;
}

.stat-label {
    color: #7f8c8d;
    font-size: 0.9em;
}

.stats-details {
    margin: -20px 0 30px 0;
}

.stats-details summary {
    cursor: pointer;
    color: #3498db;
    font-size: 0.9em;
    padding: 4px 0;
    user-select: none;
}

.stats-details summary:hover {
    text-decoration: underline;
}

.stats-tab-bar {
    display: flex;
    gap: 6px;
    margin: 12px 0 0 0;
}

.stats-tab {
    padding: 5px 14px;
    border: 1px solid #ccc;
    border-radius: 4px;
    background: #f5f5f5;
    cursor: pointer;
    font-size: 0.85em;
    color: #555;
}

.stats-tab:hover {
    background: #e8e8e8;
}

.stats-tab.active {
    background: #3498db;
    border-color: #3498db;
    color: white;
}

.stats-table {
    width: auto;
    border-collapse: collapse;
    margin: 12px auto 0 auto;
    font-size: 0.9em;
}

.stats-table th {
    background: #f0f4f8;
    text-align: left;
    padding: 8px 12px;
    border-bottom: 2px solid #dde3ea;
    font-weight: 600;
    color: #555;
}

.stats-table td {
    padding: 6px 12px;
    border-bottom: 1px solid #ccc;
}

.stats-table tr:last-child td {
    border-bottom: none;
}

.stats-table tr:hover td {
    background: #f9fbfd;
}

.std-table {
  border-collapse: collapse;
}
.std-table th, .std-table td {
  border: 1px solid black;
  padding: 4px 5px;
}


.navigation {
    display: flex;
    gap: 15px;
    margin: 40px 0;
    flex-wrap: wrap;
    max-width: 800px;
    margin-left: auto;
    margin-right: auto;
}

.nav-button {
    display: inline-block;
    padding: 15px 30px;
    background: #3498db;
    color: white;
    text-decoration: none;
    border-radius: 5px;
    font-weight: 500;
    transition: background 0.3s;
}

.break-point {
  flex-basis: 100%; /* Forces the next element onto a new line */
  height: 0; /* Keeps the break element from taking up vertical space */
}

.nav-button:hover {
    background: #2980b9;
}

.nav-button-wrap {
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 4px;
}

.nav-button-soon {
    opacity: 0.5;
    cursor: default;
}

.nav-button-soon:hover {
    background: #3498db;
}

.coming-soon {
    font-size: 0.78em;
    color: #888;
}

.nav-button-sm {
    padding: 7px 16px;
    font-size: 0.85em;
}

.nav-button.active {
    background: #2c3e50;
    cursor: default;
}

.split-button {
    position: relative;
    display: inline-flex;
    align-items: stretch;
}

.split-button-main {
    border-top-right-radius: 0;
    border-bottom-right-radius: 0;
}

.split-button-toggle {
    padding: 15px 12px;
    background: #3498db;
    color: white;
    border: none;
    border-left: 1px solid rgba(255,255,255,0.3);
    border-top-right-radius: 5px;
    border-bottom-right-radius: 5px;
    cursor: pointer;
    font-weight: 500;
    font-size: 0.9em;
    line-height: 1;
    transition: background 0.3s;
}

.split-button-toggle:hover,
.split-button-toggle[aria-expanded='true'] {
    background: #2980b9;
}

.split-button-menu {
    display: none;
    position: absolute;
    top: 100%;
    left: 0;
    margin: 4px 0 0 0;
    padding: 4px 0;
    list-style: none;
    background: white;
    border-radius: 5px;
    box-shadow: 0 4px 12px rgba(0,0,0,0.18);
    min-width: 100%;
    z-index: 100;
}

.split-button-menu.open {
    display: block;
}

.split-button-menu a {
    display: block;
    padding: 10px 18px;
    color: #2c3e50;
    text-decoration: none;
    white-space: nowrap;
}

.split-button-menu a:hover,
.split-button-menu a:focus {
    background: #f0f4f8;
    color: #2980b9;
}

.sketchbook-nav {
    display: flex;
    align-items: center;
    flex-wrap: wrap;
    gap: 5px;
    margin: 8px 0;
}

.sketchbook-nav-label {
    font-weight: bold;
    font-size: 0.85em;
    color: #555;
    margin-right: 4px;
}

.sketchbook-nav-button {
    padding: 4px 10px;
    font-size: 0.8em;
}

.artwork-table {
    width: 100%;
    background: white;
    border-radius: 8px;
    overflow: hidden;
    box-shadow: 0 2px 4px rgba(0,0,0,0.1);
}

.artwork-table thead {
    background: #34495e;
    color: white;
}

.artwork-table th {
    padding: 15px;
    text-align: left;
    font-weight: 600;
}

.artwork-table td {
    padding: 12px 15px;
    border-bottom: 1px solid #ecf0f1;
}

.artwork-table tbody tr:hover {
    background: #f8f9fa;
}

.id-cell {
    font-family: 'Courier New', monospace;
    color: #7f8c8d;
    font-size: 0.9em;
}

.title-cell {
    font-weight: 500;
    color: #2c3e50;
}

.date-cell {
    white-space: nowrap;
}

.dimensions-cell {
    font-size: 0.9em;
    color: #7f8c8d;
}

footer {
    text-align: center;
    padding: 40px 20px;
    color: #7f8c8d;
    font-size: 0.9em;
}

.footer-nav {
    display: flex;
    justify-content: center;
    flex-wrap: wrap;
    gap: 6px 20px;
    margin-bottom: 10px;
}

.footer-nav a {
    color: #7f8c8d;
    text-decoration: none;
    font-size: 0.9em;
}

.footer-nav a:hover {
    text-decoration: underline;
}

  div.gallery
  {
    display: flex;
    flex-wrap: wrap;
    justify-content: flex-start;
  }
  div.gallery-item
  {
    display: none;
    margin: 5px;
    border: 1px solid #ccc;
    width: 250px;
    position: relative;
  }
  div.gallery-item.tag-active
  {
    display: block;
  }
  my-hidden-tags
  {
    display: none;
  }

.tag-title-banner
  {
    text-align: center;
    font-size: 8em;
    font-weight: bold;
    padding: 16px 20px;
    color: #2c3e50;
  }
  div.gallery-item:hover
  {
    border: 1px solid #777;
    z-index: 50;
  }
  div.gallery-item img
  {
    width: 100%;
    height: auto;
  }
  div.gallery-item > a > img
  {
    display: block;
    transition: transform 0.2s ease;
  }
  div.gallery-item > a > img:hover
  {
    transform: scale(2);
    transform-origin: bottom center;
  }
  div.gallery-item div.desc
  {
    padding: 5px;
    text-align: center;
  }

  .thumb-buttons {
    display: flex;
    flex-wrap: wrap;
    justify-content: center;
    gap: 5px;
    padding: 5px;
  }

  .thumb-button {
    display: inline-block;
    position: relative;
    border: 2px solid #ccc;
    border-radius: 4px;
    transition: border-color 0.2s;
  }

  .thumb-button:hover {
    border-color: #3498db;
  }

  .thumb-button img:not(.thumb-preview) {
    display: block;
    width: 40px;
    height: 40px;
  }

  .thumb-button img.thumb-preview {
    display: none;
    position: absolute;
    bottom: 44px;
    left: 50%;
    transform: translateX(-50%);
    max-width: 280px;
    max-height: 280px;
    width: auto;
    height: auto;
    border: 2px solid #3498db;
    border-radius: 4px;
    box-shadow: 0 4px 16px rgba(0,0,0,0.45);
    background: white;
    z-index: 200;
    pointer-events: none;
  }

  .thumb-button:hover img.thumb-preview {
    display: block;
  }

  body.no-thumb-hover .thumb-button:hover img.thumb-preview {
    display: none;
  }

  body.no-image-hover div.gallery-item > a > img:hover {
    transform: none;
  }

  .site-notice {
    text-align: center;
    background: #0000CC;
    color: #ffffff;
    padding: 10px 20px;
    font-size: 0.85em;
    border-bottom: 1px solid #d0c8b8;
    letter-spacing: 0.02em;
  }

  .page-controls {
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 4px;
    padding: 6px 16px;
    background: #f0f4f8;
    border-bottom: 1px solid #d0d8e0;
    font-size: 0.875em;
    color: #444;
  }

  .page-controls-row {
    display: flex;
    align-items: center;
    flex-wrap: wrap;
    justify-content: center;
    gap: 4px 14px;
  }

  .page-controls-label {
    font-weight: bold;
    color: #555;
  }

  .page-controls label {
    cursor: pointer;
    display: flex;
    align-items: center;
    gap: 4px;
    white-space: nowrap;
  }

  #type-filter-checkboxes {
    display: inline-flex;
    flex-wrap: wrap;
    gap: 2px 10px;
    align-items: center;
  }

.small-button {
    /* Sizing & Alignment */
    display: inline-block;
    padding: 1px 4px; /* Adjust padding to control size */
    font-size: 26px;
    text-align: center;
    text-decoration: none; /* Removes underline for <a> elements */
    cursor: pointer; /* Changes cursor to a hand on hover */
    
    /* Appearance */
    background-color: #007bff; /* Primary brand color */
    color: white;
    border: none;
    border-radius: 5px; /* Rounded corners */
    box-shadow: 0 2px 4px rgba(0, 0, 0, 0.2); /* Subtle shadow */
    transition: background-color 0.3s ease, transform 0.2s ease, box-shadow 0.3s ease; /* Smooth transitions for effects */

    /* Ensures consistent font rendering */
    font-family: sans-serif;
    -webkit-font-smoothing: antialiased;
}

.small-button:hover {
    background-color: #0056b3; /* Darker color on hover */
    transform: translateY(-2px); /* Lifts the button slightly */
    box-shadow: 0 4px 8px rgba(0, 0, 0, 0.3); /* Increases shadow on hover */
}

.small-button:active {
    transform: translateY(0); /* Pushes button down when clicked */
    box-shadow: 0 1px 2px rgba(0, 0, 0, 0.2);
}





.find-form {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: 12px;
    margin-top: 16px;
}

.find-form select,
.find-form input[type='number'] {
    font-size: 1.1em;
    padding: 10px 14px;
    border: 1px solid #ccc;
    border-radius: 5px;
    background: white;
    color: #333;
    min-width: 130px;
}

.find-form input[type='number'] {
    min-width: 110px;
    max-width: 140px;
}

.find-form button {
    font-size: 1.1em;
    padding: 10px 28px;
    background: #3498db;
    color: white;
    border: none;
    border-radius: 5px;
    cursor: pointer;
    font-weight: 500;
    transition: background 0.2s;
}

.find-form button:hover {
    background: #2980b9;
}

/* Credits page — per-person blocks */
.credit-person {
    margin-bottom: 2em;
}

.credit-name {
    font-weight: bold;
    font-size: 1.15em;
}

.credit-role {
    font-style: italic;
    color: #555;
    margin-bottom: 0.5em;
}

.credit-blurb {
    margin-bottom: 0.5em;
}

.credit-links {
    font-size: 0.95em;
}

.credit-links a {
    color: #3498db;
    text-decoration: none;
}

.credit-links a:hover {
    text-decoration: underline;
}

@media (max-width: 768px) {
    .artwork-table {
        font-size: 0.9em;
    }

    .artwork-table th, .artwork-table td {
        padding: 8px 10px;
    }

    h1 {
        font-size: 2em;
    }
}

/* Lightbox */
.lightbox { display:none; position:fixed; top:0; left:0; right:0; bottom:0; z-index:1000; align-items:center; justify-content:center; }
.lightbox.active { display:flex; }
.lightbox-overlay { position:absolute; top:0; left:0; right:0; bottom:0; background:rgba(0,0,0,0.88); cursor:pointer; }
.lightbox-container { position:relative; z-index:1001; display:flex; align-items:center; gap:10px; max-width:95vw; max-height:95vh; }
.lightbox-content { display:flex; flex-direction:column; align-items:center; max-width:85vw; }
.lightbox-img { max-width:85vw; max-height:80vh; object-fit:contain; border:2px solid #555; border-radius:4px; }
.lightbox-caption { color:#ddd; padding:10px 5px 0; text-align:center; font-size:0.9em; max-width:85vw; }
.lightbox-prev, .lightbox-next { background:rgba(255,255,255,0.15); border:none; color:white; font-size:2.5rem; cursor:pointer; padding:15px 10px; border-radius:4px; user-select:none; flex-shrink:0; transition:background 0.2s; }
.lightbox-prev:hover, .lightbox-next:hover { background:rgba(255,255,255,0.3); }
.lightbox-close { position:absolute; top:-2.5rem; right:0; background:none; border:none; color:white; font-size:1.8rem; cursor:pointer; padding:0 5px; opacity:0.8; transition:opacity 0.2s; }
.lightbox-close:hover { opacity:1; }
.lightbox-view-nav { display:flex; align-items:center; gap:8px; padding:6px 0 0; }
.lightbox-view-prev, .lightbox-view-next { background:rgba(255,255,255,0.15); border:none; color:white; font-size:1.2rem; cursor:pointer; padding:3px 10px; border-radius:4px; user-select:none; transition:background 0.2s; }
.lightbox-view-prev:hover, .lightbox-view-next:hover { background:rgba(255,255,255,0.3); }
.lightbox-view-label { color:#ccc; font-size:0.85em; min-width:100px; text-align:center; }

.show-section-heading { color: #2c3e50; font-size: 1.8em; margin: 32px 0 8px 0; padding-bottom: 4px; border-bottom: 1px solid #ccc; }
.show-section-heading:first-of-type { margin-top: 16px; }

.show-year { color: #2c3e50; font-size: 1.15em; font-weight: 600; margin: 18px 0 4px 0; }
.show-year-first { margin-top: 10px; }

.show-index { list-style: none; padding-left: 32px; margin-bottom: 12px; }
.show-index li { padding: 4px 0; }
.show-index li a { color: #3498db; text-decoration: none; font-weight: 500; }
.show-index li a:hover { text-decoration: underline; }
.show-index-gallery { color: #555; }
.show-index-dates { color: #7f8c8d; font-size: 0.9em; }
.show-index-count { color: #7f8c8d; font-size: 0.9em; }

.show-meta { margin: 20px 0 30px 0; display: grid; grid-template-columns: max-content 1fr; column-gap: 16px; row-gap: 6px; max-width: 900px; }
.show-meta dt { font-weight: 600; color: #555; }
.show-meta dd { color: #333; }

.show-invites { margin-bottom: 24px; }

.show-section { margin: 14px 0; background: white; border: 1px solid #e0e0e0; border-radius: 6px; padding: 8px 14px; }
.show-section summary { cursor: pointer; color: #3498db; font-weight: 500; padding: 6px 0; user-select: none; }
.show-section summary:hover { text-decoration: underline; }
.show-section[open] summary { border-bottom: 1px solid #eee; margin-bottom: 10px; }
";

    await File.WriteAllTextAsync(Path.Combine(_outputDirectory, "style.css"), css);
  }
}
