namespace Dagligvarer.Core;

/// <summary>
/// Sorterer tilbud i madkategorier ud fra varenavnet og frasorterer ting der ikke er mad
/// (vaskemaskiner, tøj, elektronik osv.). Ingen database: kategorien regnes ud når den skal bruges.
/// </summary>
public static class Categorizer
{
    public const string Other = "Øvrigt";

    /// <summary>Rækkefølgen er vigtig: første kategori der matcher vinder (fx skal "mælkechokolade" være sødt, ikke mejeri).</summary>
    private static readonly (string Name, string[] Words)[] Rules =
    [
        ("Snacks og slik", ["chokolade", "slik", "vingummi", "lakrids", "chips", "popcorn", "nødder", "peanuts", "kiks", "cookies", "bolsjer", "karamel", "is", "vaffel", "flødebolle", "marcipan", "müslibar", "bar"]),
        ("Drikkevarer", ["sodavand", "cola", "juice", "saft", "vand", "danskvand", "kaffe", "te", "kakao", "øl", "vin", "cider", "spiritus", "vodka", "gin", "whisky", "rom", "likør", "energidrik", "smoothie", "mousserende", "champagne", "cava", "sodastream", "fanta", "sprite", "pepsi"]),
        ("Brød og kager", ["brød", "rugbrød", "toast", "boller", "bolle", "rundstykker", "kage", "kager", "croissant", "wienerbrød", "tebirkes", "flute", "baguette", "tortilla", "wraps", "pita", "knækbrød", "hamburgerboller", "pandekager", "muffin", "brownie"]),
        ("Kød og pålæg", ["kød", "oksekød", "svinekød", "kylling", "kyllingebryst", "bryst", "bacon", "pølse", "pølser", "skinke", "pålæg", "leverpostej", "postej", "medister", "frikadeller", "bøf", "bøffer", "steg", "koteletter", "kotelet", "mørbrad", "culotte", "flæsk", "hakket", "spegepølse", "salami", "and", "kalkun", "lam", "nuggets", "hotdog", "hamburgerryg", "ribben", "schnitzel"]),
        ("Fisk og skaldyr", ["fisk", "laks", "torsk", "rødspætte", "sild", "makrel", "tun", "rejer", "rejen", "muslinger", "fiskefilet", "fiskefrikadeller", "hellefisk", "ørred", "krabbe", "hummer"]),
        ("Mejeri og æg", ["mælk", "yoghurt", "skyr", "ost", "smør", "fløde", "æg", "creme fraiche", "cremefraiche", "kefir", "kærnemælk", "minimælk", "letmælk", "sødmælk", "kvark", "hytteost", "mozzarella", "feta", "parmesan", "margarine", "remoulade", "ymer", "tykmælk", "mayonnaise"]),
        ("Frugt og grønt", ["æble", "æbler", "banan", "bananer", "appelsin", "appelsiner", "citron", "pære", "pærer", "drue", "druer", "jordbær", "blåbær", "hindbær", "melon", "ananas", "mango", "avocado", "kiwi", "tomat", "tomater", "agurk", "salat", "kartoffel", "kartofler", "løg", "gulerod", "gulerødder", "peberfrugt", "broccoli", "blomkål", "kål", "spinat", "svampe", "champignon", "porre", "squash", "aubergine", "radiser", "selleri", "frugt", "grønt", "grøntsager", "nektarin", "fersken", "clementiner", "mandariner", "hvidløg", "ingefær", "krydderurter", "urter", "majs", "ærter", "bønner", "rødbeder", "persille", "basilikum"]),
        ("Frost og færdigretter", ["frost", "frossen", "frosne", "pizza", "lasagne", "færdigret", "færdigretter", "suppe", "nudler", "pommes", "pomfritter", "dybfrost", "burger", "dumplings", "sushi", "tærte"]),
        ("Kolonial", ["pasta", "spaghetti", "ris", "mel", "sukker", "salt", "peber", "olie", "olivenolie", "eddike", "ketchup", "sennep", "dressing", "sauce", "pesto", "havregryn", "müsli", "morgenmad", "cornflakes", "cereal", "marmelade", "honning", "nutella", "pålægschokolade", "peanutbutter", "konserves", "dåse", "hakkede tomater", "bønner", "linser", "kikærter", "bouillon", "fond", "krydderi", "krydderier", "bagepulver", "gær", "knorr", "mayo", "pasta", "couscous", "bulgur", "tomatpuré", "kokosmælk", "sirup", "pålægssalat", "hummus", "oliven", "agurkesalat", "rugmel", "hvedemel", "havredrik", "sojadrik", "mandeldrik"]),
        ("Husholdning og pleje", ["toiletpapir", "køkkenrulle", "vaskemiddel", "opvaskemiddel", "skyllemiddel", "rengøring", "shampoo", "balsam", "tandpasta", "tandbørste", "sæbe", "håndsæbe", "deodorant", "bleer", "vådservietter", "affaldsposer", "affaldspose", "frysepose", "frysereposer", "bagepapir", "stanniol", "film", "servietter", "hårfarve", "creme", "barbergel", "bind", "tamponer", "opvasketabs", "maskinopvask", "gulvvask", "universalspray", "toiletrens", "karklude", "svampe", "batterier", "fyrfadslys", "lys"]),
    ];

    /// <summary>Hvis varenavnet indeholder et af disse ord er det ikke mad og ikke dagligvarer, og tilbuddet vises ikke.</summary>
    private static readonly string[] NonFood =
    [
        "vaskemaskine", "tørretumbler", "opvaskemaskine", "køleskab", "fryser", "ovn", "mikrobølgeovn", "støvsuger", "robotstøvsuger", "airfryer", "kaffemaskine", "blender", "brødrister", "strygejern", "ventilator", "varmeblæser", "emhætte", "komfur",
        "tv", "fjernsyn", "computer", "laptop", "tablet", "mobiltelefon", "smartphone", "telefon", "hovedtelefoner", "høretelefoner", "højttaler", "kamera", "printer", "oplader", "powerbank", "headset", "smartwatch",
        "jakke", "bukser", "jeans", "trøje", "t-shirt", "skjorte", "kjole", "nederdel", "strømper", "sokker", "undertøj", "sko", "støvler", "sandaler", "hue", "handsker", "halstørklæde", "pyjamas", "badetøj", "bh", "trusser", "leggings", "sweatshirt", "hættetrøje", "fleece",
        "sengetøj", "dyne", "pude", "tæppe", "gardin", "gardiner", "håndklæde", "håndklæder", "madras", "lampe", "spejl", "bordlampe", "tæpper", "betræk", "sengelinned",
        "boremaskine", "værktøj", "skruetrækker", "hammer", "sav", "håndsav", "slibemaskine", "højtryksrenser", "plæneklipper", "græstrimmer", "havemøbel", "havemøbler", "parasol", "trampolin", "drivhus", "plantekasse", "krukke", "havestol", "havebord",
        "legetøj", "lego", "dukke", "puslespil", "brætspil", "fodbold", "cykel", "cykelhjelm", "løbehjul", "rygsæk", "kuffert", "taske", "pung", "bilvask", "bilpleje", "dæk", "fiskestang", "telt", "sovepose",
        "gryde", "gryder", "pande", "stegepande", "bestik", "kniv", "knive", "tallerken", "tallerkener", "service", "krus", "termokande", "skærebræt", "bageform", "opbevaringsboks", "hylde", "reol", "skab", "stol", "bord", "sofa", "seng",
        "julepynt", "juletræ", "julelys", "gaveindpakning", "gaveæske", "kalender", "bog", "bøger", "blomst", "blomster", "buket", "potteplante", "stueplante", "orkidé", "dekoration", "vækkeur", "lyspære",
    ];

    /// <summary>Alle kategorier i den rækkefølge de vises i UI'et.</summary>
    public static IReadOnlyList<string> All { get; } = Rules.Select(r => r.Name).Append(Other).ToList();

    /// <summary>Er tilbuddet mad eller almindelige dagligvarer (ikke fx en vaskemaskine)?</summary>
    public static bool IsGrocery(string heading) => Tokens(heading).All(t => !NonFood.Any(w => Hit(t, w, allowPrefix: false)));

    /// <summary>Finder kategorien for et varenavn. Ukendte varer lander i "Øvrigt".</summary>
    public static string For(string heading)
    {
        var tokens = Tokens(heading);
        foreach (var (name, words) in Rules)
            if (tokens.Any(t => words.Any(w => Hit(t, w, allowPrefix: true)))) return name;
        return Other;
    }

    private static string[] Tokens(string text) =>
        TextKey.Normalize(text).Split(' ', StringSplitOptions.RemoveEmptyEntries);

    // Korte ord der kun må matche som helt ord, ellers rammer de tilfældige ord ("is" i "paris").
    private static readonly HashSet<string> ExactOnly = ["te", "is", "and", "bar", "rom", "gin", "lam", "tv", "bh"];

    /// <summary>
    /// Danske sammensatte ord har hovedordet til sidst ("letmælk", "kyllingebryst"), så vi matcher slutningen af ordet.
    /// Længere ord må også matche starten ("kyllingefilet"). Ord på under 4 bogstaver kræver hele ordet eller en lang nok forstavelse.
    /// </summary>
    private static bool Hit(string token, string word, bool allowPrefix)
    {
        if (token == word) return true;
        if (word.Length < 4)
            return !ExactOnly.Contains(word) && token.Length >= word.Length + 3 && token.EndsWith(word, StringComparison.Ordinal);
        return token.EndsWith(word, StringComparison.Ordinal) || (allowPrefix && token.StartsWith(word, StringComparison.Ordinal));
    }
}
