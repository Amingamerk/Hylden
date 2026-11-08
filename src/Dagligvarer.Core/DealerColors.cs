namespace Dagligvarer.Core;

/// <summary>Farve pr. kæde. Tjeks egen farve bruges hvis den findes.</summary>
public static class DealerColors
{
    private static readonly Dictionary<string, string> Known = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Lidl"] = "#0050AA", ["Rema 1000"] = "#1B3E8E", ["SuperBrugsen"] = "#D3151E",
        ["Føtex"] = "#00407A", ["Normal"] = "#E4006D", ["Netto"] = "#E8B100", ["Bilka"] = "#00A1DE",
        ["Meny"] = "#C8102E", ["Coop 365"] = "#2F7D32", ["Kvickly"] = "#7A1E3A", ["Spar"] = "#00843D",
    };
    private static readonly string[] Fallback = ["#3D5A80", "#9C6644", "#5F0F40", "#0F7173", "#7B2CBF", "#B5651D"];

    public static string For(string dealer, string? apiColor = null)
    {
        if (Known.TryGetValue(dealer, out var c)) return c;
        if (!string.IsNullOrWhiteSpace(apiColor) && apiColor != "#333333") return apiColor;
        return Fallback[Math.Abs(StringComparer.OrdinalIgnoreCase.GetHashCode(dealer)) % Fallback.Length];
    }
}
