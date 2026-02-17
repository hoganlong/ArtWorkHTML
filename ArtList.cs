
using System.Diagnostics.Tracing;
using System.Net.Http.Headers;

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

    public void AddBucketFile(string dir, string name, string ext, DateTime? lastModified)
    {
      var listElement = artworks.Where(x => x.Value.iFileName == name).FirstOrDefault();
      var artwork = new Artwork(name);

      if (listElement.Key == null)
      {
        // file in bucket that doesn't match any of our artwork names create artwork for it.
        artwork.states |= StatesType.noDB; // Set noDB state
        artwork.ctDate = lastModified ?? DateTime.Now;   // Set the ctDate to the last modified date of the file in the bucket
        artwork.iFileName = name;  // set the iFileName to the name of the file we found in the bucket so we can find it later if we need to
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
    }
  } // ArtList
  
  public class Artwork
  {
    static readonly string baseURL = "https://keithlong-art-photos.s3.us-east-1.amazonaws.com/";

    // Data elements
    public string id = new("");
    public string iFileName = new("");
    public string title = new("");
    public string series = new("");
    public DateTime ctDate;
    public string medium = new("");
    public string dimensions = new("");
    public string foldedDimensions = new("");
    public string location = new("");
    public string notes = new("");
    public string humanId = new("");
    public string image_ids = new("");
    public List<string> errors = new();

    // Artwork image IDs for different views
    public int? backId = null;
    public int? frontId = null;
    public int? paperId = null;
    public int? polaroidId = null;

    public StatesType states=StatesType.NoImage; 

    // Direved utility elements
    public string tifURL = new("");
    public string jpgURL = new("");

    public Artwork(string id, string iFileName, string title, string series, DateTime ctDate, string medium,string dimensions, string foldedDimensions,
       string location, string notes, string humanId, string image_ids, int? backId = null, int? frontId = null, int? paperId = null, int? polaroidId = null)
    {
      this.id = id ?? "";
      this.iFileName = iFileName ?? "";
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
      this.backId = backId;
      this.frontId = frontId;
      this.paperId = paperId;
      this.polaroidId = polaroidId;

      if (string.IsNullOrEmpty(iFileName))
      { // set no image state if we don't have an image file name
        this.states |= StatesType.NoImage;
        // Use Front image as fallback if available
        if (frontId.HasValue)
        {
          this.states &= ~StatesType.NoImage;
          jpgURL = $"{baseURL}atch/artwork_{frontId.Value}_large.jpg";
          this.errors.Add("Bucket image not found");
        }
      }
      else
      { // Remove no image state if we have an image file name
        this.states &= ~StatesType.NoImage;
        tifURL = baseURL + iFileName + ".tif";
        jpgURL = baseURL + "jpg/" + iFileName + ".jpg";
      }

    }

    public Artwork(string filename)
    {
      this.id = "";
      this.iFileName = filename;
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
      tifURL = baseURL + iFileName + ".tif";
      jpgURL = baseURL + "jpg/" + iFileName + ".jpg";

      this.errors.Add($"Was found on server and not DB");

    }
  } // Artwork
}  // ArtWorkHTML