using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dagligvarer.Core;

/// <summary>En fysisk butik tæt på brugeren, hentet fra Tjek.</summary>
public class Store
{
    public string Id { get; set; } = "";
    public string DealerId { get; set; } = "";
    public string DealerName { get; set; } = "";
    public string Street { get; set; } = "";
    public string City { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int DistanceMeters { get; set; }
    public string BrandColor { get; set; } = "#333333";
}

/// <summary>Et tilbud fra en tilbudsavis.</summary>
public class Offer
{
    public string TjekId { get; set; } = "";
    public string DealerId { get; set; } = "";
    public string DealerName { get; set; } = "";
    public string Heading { get; set; } = "";
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public decimal? PrePrice { get; set; }
    /// <summary>Pris pr. kg, liter eller stk. hvis mængden kendes.</summary>
    public decimal? UnitPrice { get; set; }
    public string? UnitLabel { get; set; }
    public string? QuantityText { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime ValidTo { get; set; }
    public string? ImageUrl { get; set; }
    /// <summary>Normaliseret navn, bruges til at samle prishistorik for samme vare.</summary>
    public string ProductKey { get; set; } = "";
    public DateTime FetchedAt { get; set; }
}

/// <summary>En linje på indkøbslisten, fx "mælk". Gemmes i browserens localStorage.</summary>
public class ShoppingListItem
{
    public int Id { get; set; }
    public string Query { get; set; } = "";
    public int Quantity { get; set; } = 1;
    public bool Done { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Hvor og hvor langt der søges. Koordinatet er med vilje rundet af (to decimaler, ca. 1 km),
/// så den præcise adresse aldrig står i repoet.
/// </summary>
public class DataLocation
{
    public string Label { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int RadiusMeters { get; set; }
}

/// <summary>Indholdet af data/latest.json: det seneste hentede billede af butikker og tilbud.</summary>
public class OfferData
{
    public DateTime FetchedAt { get; set; }
    public DataLocation Location { get; set; } = new();
    public List<Store> Stores { get; set; } = [];
    public List<Offer> Offers { get; set; } = [];
    /// <summary>Datoer (yyyy-MM-dd) der har en fil i data/snapshots/. Nyeste sidst.</summary>
    public List<string> SnapshotDates { get; set; } = [];
}

/// <summary>Billigste pris for én vare i én kæde på én dag. Grundlaget for grafen.</summary>
public class SnapshotRow
{
    public string ProductKey { get; set; } = "";
    public string Heading { get; set; } = "";
    public string DealerName { get; set; } = "";
    public decimal Price { get; set; }
    public decimal? UnitPrice { get; set; }
    public string? UnitLabel { get; set; }
}

/// <summary>Indholdet af data/snapshots/yyyy-MM-dd.json.</summary>
public class DailySnapshot
{
    public string Date { get; set; } = "";
    public List<SnapshotRow> Rows { get; set; } = [];
}

/// <summary>
/// JSON-opsætning delt af hentningen og appen. Source generator i stedet for refleksion,
/// så det virker uændret når WebAssembly-appen "trimmes" ved publicering.
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = false,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(OfferData))]
[JsonSerializable(typeof(DailySnapshot))]
[JsonSerializable(typeof(List<ShoppingListItem>))]
public partial class HyldenJson : JsonSerializerContext;
