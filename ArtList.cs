
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
    undef6 = 64
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

    public void AddBucketFile(string dir, string name, string ext)
    {
      var listElement = artworks.Where(x => x.Value.iFileName == name).FirstOrDefault();
      
      if (listElement.Key == null)
      {
        // file in bucket that doesn't match any of our artwork names create artwork for it.
        artworks["unknow "+dupNumber.ToString()] = new Artwork(name);
        dupNumber++;
      }
      else
      {
        // artwork for out name
        var artwork = listElement.Value;

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


    }



  }
  

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

    public StatesType states=StatesType.NoImage; 

    // Direved utility elements
    public string tifURL = new("");
    public string jpgURL = new("");

    public Artwork(string id, string iFileName, string title, string series, DateTime ctDate, string medium,string dimensions, string foldedDimensions,
       string location, string notes, string humanId, string image_ids)
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
      
      if (string.IsNullOrEmpty(iFileName))
      { // set no image state if we don't have an image file name
        this.states |= StatesType.NoImage;
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




  }
}