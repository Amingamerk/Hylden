namespace Dagligvarer.Core;

/// <summary>
/// Indstillinger for hentningen. Placeringen er med vilje grov: "Odense" med et koordinat rundet
/// til to decimaler. Den rigtige adresse må aldrig stå i repoet.
/// </summary>
public class AppOptions
{
    public string Label { get; set; } = "Odense";
    public double Latitude { get; set; } = 55.40;
    public double Longitude { get; set; } = 10.38;
    public int RadiusMeters { get; set; } = 1000;
    /// <summary>Kæder der skal med. Tom liste = alle kæder inden for radius.</summary>
    public List<string> Dealers { get; set; } = ["Lidl", "Rema 1000", "SuperBrugsen", "Normal", "Føtex"];
    public string TjekBaseUrl { get; set; } = "https://squid-api.tjek.com/v2/";
    /// <summary>Valgfri. Tjek kræver ikke altid en nøgle til læsning.</summary>
    public string? TjekApiKey { get; set; }
}
