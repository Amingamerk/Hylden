using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Dagligvarer.Core;

/// <summary>Gør varenavne sammenlignelige på tværs af uger og kæder.</summary>
public static partial class TextKey
{
    public static string Normalize(string text)
    {
        var lower = text.Trim().ToLower(new CultureInfo("da-DK"));
        lower = NonWord().Replace(lower, " ");
        return Spaces().Replace(lower, " ").Trim();
    }

    /// <summary>True hvis alle ord i søgningen findes i varenavnet (fx "hakket oksekød").</summary>
    public static bool Matches(string heading, string query)
    {
        var h = " " + Normalize(heading) + " ";
        var words = Normalize(query).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length > 0 && words.All(w => h.Contains(w));
    }

    [GeneratedRegex(@"[^\p{L}\p{N}%]+")] private static partial Regex NonWord();
    [GeneratedRegex(@"\s+")] private static partial Regex Spaces();
}
