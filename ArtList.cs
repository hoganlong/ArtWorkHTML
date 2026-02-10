
using System.Diagnostics.Tracing;
using System.Net.Http.Headers;

namespace ArtWorkHTML
{
  [Flags]
  public enum StatesType
  {
    None = 0,
    NoImage = 1,
    undef1 = 2,
    undef2 = 4,
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



  }
}