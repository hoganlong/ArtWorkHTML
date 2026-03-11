
using System.ComponentModel;
using System.Diagnostics.Tracing;
using System.Net.Http.Headers;
using Amazon.Util.Internal;

namespace ArtWorkHTML
{
  [Flags]
  public enum StatesType
  {
    None = 0,
    NoImage = 1,
    jpgFound = 2,
    tifFound = 4,

    undef3 = 8,
    undef4 = 16,
    undef5 = 32,
    noDB = 64
  }
  
  [Flags]
  public enum ArtType
  {
    None = 0,
    Sketch = 1,

    undef1 = 2,
    undef2 = 4,
    undef3 = 8,
    undef4 = 16,
    undef5 = 32,
    noDB = 64
  }



  public class ArtList
  {
    public System.Collections.Generic.Dictionary<string,Artwork> artworks;
    static int dupNumber = 0;
    public ArtList()
    {
      artworks = new System.Collections.Generic.Dictionary<string, Artwork>();
    }

    public void AddArtwork(Artwork artwork)
    {
      if (artwork != null && !string.IsNullOrEmpty(artwork.humanId))
      {
        if (artworks.TryGetValue(artwork.humanId, out var existingArtwork))
        {
          artwork.errors.Add($"Duplicate humanId '{artwork.humanId}'.");
          existingArtwork.errors.Add($"Duplicate humanId '{artwork.humanId}'.");

          artworks[artwork.humanId+" dup"+dupNumber.ToString()] = artwork;
          dupNumber++;
        }
        else
        {
          artworks[artwork.humanId] = artwork;
        }
      }
    }

    public void AddSketchBucketFile(string dir, string name, string ext, DateTime? lastModified,bool removeServerError = false)
    {
      Artwork a = this.AddBucketFile(dir, name, ext, lastModified, removeServerError);

      a.myType = ArtType.Sketch;

      var nameParts = name.Split('_'); // Expecting format like "KLA_1_1.tif" for sketchbook files 
      char lastChar = nameParts[2][^1];
      string letterPart = "";
      if (char.IsLetter(lastChar))
      {
        letterPart = lastChar.ToString();
        nameParts[2] = nameParts[2][..^1];
      }
      if (nameParts.Length >= 3 && int.TryParse(nameParts[1], out int sketchbookNumber) && int.TryParse(nameParts[2].Split('.')[0], out int pageNumber))
      {
        a.pageNumber = pageNumber;
        a.sketchbookNumber = sketchbookNumber;
        a.humanId = $"{sketchbookNumber}_{pageNumber}{letterPart}";
      } 
      else
      {
        a.errors.Add($"Filename '{name}' does not match expected sketchbook format 'KLA_sketchbookNumber_pageNumber.ext'.");
      }
    }

    public Artwork AddBucketFile(string dir, string name, string ext, DateTime? lastModified,bool removeServerError = false)
    {
      var listElement = artworks.FirstOrDefault(x => x.Value.fileName == name);
      Artwork artwork = new Artwork(dir,name,removeServerError);

      if (listElement.Key == null)
      {
        // file in bucket that doesn't match any of our artwork names create artwork for it.
        artwork.states |= StatesType.noDB; // Set noDB state
        artwork.ctDate = lastModified ?? DateTime.Now;   // Set the ctDate to the last modified date of the file in the bucket
        artwork.fileName = name;  // set the iFileName to the name of the file we found in the bucket so we can find it later if we need to
        artworks["unknow "+dupNumber.ToString()] = artwork;
        dupNumber++;
      }
      else
      {
        // artwork for out name
        artwork = listElement.Value;
      }

      if (ext == "jpg")
      {
        // should check if in correct (expected) location in bucket, but for now just set the state
        artwork.states |= StatesType.jpgFound; // Set jpg found state
      }
      else if (ext == "tif")
      {
        // should check if in correct (expected) location in bucket, but for now just set the state
        artwork.states |= StatesType.tifFound; // Set tif found state
      }
      return artwork;
    }
  } // ArtList
  
  public class Artwork
  {
    static readonly string baseURL = "https://keithlong-art-photos.s3.us-east-1.amazonaws.com/";

    // Data elements
    public ArtType myType = ArtType.None;
    public string id = new("");
    public string fileName = new("");
    public string title = new("");
    public string series = new("");
    public DateTime ctDate;
    public string medium = new("");
    public string dimensions = new("");
    public string foldedDimensions = new("");
    public string location = new("");
    public string notes = new("");     // notes - ? probably will be turned into archiveNotes and pubNotes for all artworks.
    public string humanId = new("");
    public string image_ids = new("");
    public string typeCode = new("");
    public List<string> errors = [];

    // Data elements for sketch
    // id
    // ctDate
    // location
    public string people = new("");
    // medium
    public int sketchbookNumber = 0;
    public int pageNumber = 0;
    public string? artworkID = null;
    public string pubNotes = new("");
    // filename

    // Artwork image IDs for different views
    public int[]? backId = null;
    public int[]? frontId = null;
    public int[]? paperId = null;
    public int[]? polaroidId = null;

    // Artwork filenames for different views
    public string[]? backFileName = null;
    public string[]? frontFileName = null;

    public StatesType states=StatesType.NoImage; 

    // Direved utility elements
    public string tifURL = new("");
    public string jpgURL = new("");

    public string MakeJPGURL(string filename)
    {
      return $"{baseURL}jpg/{filename}.jpg";
    }


    public Artwork(string id, string iFileName, string title, string series, DateTime ctDate, string medium,string dimensions, string foldedDimensions, string location, string notes, string humanId, 
       string image_ids,string typeCode, int[]? backId = null, int[]? frontId = null, int[]? paperId = null, int[]? polaroidId = null, string[]? backFileName = null, string[]? frontFileName = null )
    {
      this.id = id ?? "";
      this.fileName = iFileName ?? "";
      this.title = title ?? "";
      this.series = series ?? "";
      this.ctDate = ctDate;
      this.medium = medium ?? "";
      this.dimensions = dimensions ?? "";
      this.foldedDimensions = foldedDimensions ?? "";
      this.location = location ?? "";
      this.notes = notes ?? "";
      this.humanId = humanId ?? "";
      this.image_ids = image_ids ?? "";
      this.typeCode = typeCode ?? "";
      this.backId = backId;
      this.frontId = frontId;
      this.paperId = paperId;
      this.polaroidId = polaroidId;
      this.backFileName = backFileName;
      this.frontFileName = frontFileName; 

      if (string.IsNullOrEmpty(iFileName))
      { // set no image state if we don't have an image file name
        this.states |= StatesType.NoImage;
        // Use Front image as fallback if available
        if (frontId is not null)
        {
          this.states &= ~StatesType.NoImage;
          jpgURL = $"{baseURL}atch/artwork_{frontId[0]}_large.jpg";
          this.errors.Add("Bucket image not found");
        }
      }
      else
      { // Remove no image state if we have an image file name
        this.states &= ~StatesType.NoImage;
        tifURL = baseURL + iFileName + ".tif";
        jpgURL = MakeJPGURL(iFileName);
      }
    }

    // Contructor for sketchbook pages
    public Artwork(string id, DateTime ctDate, string location, string people, string medium, int sketchbookNumber, int pageNumber, string? artworkID, string pubNotes, string fileName)
    {
      this.myType = ArtType.Sketch;
      this.id = id;
      this.ctDate = ctDate;
      this.location = location;
      this.people = people;
      this.medium = medium;
      this.sketchbookNumber = sketchbookNumber;
      this.pageNumber = pageNumber;
      this.artworkID = artworkID;
      this.pubNotes = pubNotes;
      this.fileName = fileName;
      char lastChar = fileName[^1];
      this.humanId = $"KLA_{sketchbookNumber}_{pageNumber}{(char.IsLetter(lastChar)? lastChar.ToString() : "")}";
       if (string.IsNullOrEmpty(fileName))
      { // set no image state if we don't have an image file name
        this.states |= StatesType.NoImage;
        // Use Front image as fallback if available
        this.errors.Add("Bucket image not found");
      }
      else
      { // Remove no image state if we have an image file name
        this.states &= ~StatesType.NoImage;
      }
      // the following is not "tested" in code and should 
      tifURL = baseURL + "scans/" + fileName + ".tif";
      jpgURL = baseURL + "scans/jpg/" + fileName + ".jpg";
    }

    public Artwork(string filename)
    {
      this.id = "";
      this.fileName = filename;
      this.title = "";
      this.series = "";
      this.ctDate = DateTime.Now;
      this.medium = "";
      this.dimensions =  "";
      this.foldedDimensions = "";
      this.location =  "";
      this.notes =  "";
      this.humanId =  "";
      this.image_ids =  "";

      this.states &= ~StatesType.NoImage;
      // the following is not "tested" in code and should 
      tifURL = baseURL + fileName + ".tif";
      jpgURL = baseURL + fileName + ".jpg";

      this.errors.Add($"Was found on server and not DB");

    }
    
    // This constructor is used when we only have the filename from the bucket and no other information about the artwork. It creates an artwork with default values for all other properties and sets the state to indicate that it was found in the bucket but not in the database.
    public Artwork(string path, string filename,bool removeServerError = false)
    {
      this.id = "";
      this.fileName = filename;
      this.title = "";
      this.series = "";
      this.ctDate = DateTime.Now;
      this.medium = "";
      this.dimensions =  "";
      this.foldedDimensions = "";
      this.location =  "";
      this.notes =  "";
      this.humanId =  "";
      this.image_ids =  "";

      this.states &= ~StatesType.NoImage;
      // the following is not "tested" in code and should 
      tifURL = baseURL + path + fileName + ".tif";
      jpgURL = baseURL + path + "jpg/" + fileName + ".jpg";

      if (!removeServerError)
        this.errors.Add($"Was found on server and not DB");

    }
  } // Artwork
}  // ArtWorkHTML