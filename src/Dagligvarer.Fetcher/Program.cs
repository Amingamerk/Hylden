using System.Text.Json;
using Dagligvarer.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

// Hentning af tilbud. Køres dagligt af GitHub Actions (se .github/workflows/deploy.yml),
// men kan også køres lokalt:  dotnet run --project src/Dagligvarer.Fetcher -- <udmappe>
// Skriver <udmappe>/latest.json og <udmappe>/snapshots/<dato>.json.

var outDir = args.FirstOrDefault() ?? Path.Combine("src", "Dagligvarer.Web", "wwwroot", "data");
var options = new AppOptions { TjekApiKey = Environment.GetEnvironmentVariable("TJEK_API_KEY") };

using var loggerFactory = LoggerFactory.Create(b => b.AddSimpleConsole(o => o.SingleLine = true));
var log = loggerFactory.CreateLogger("Fetcher");

using var http = new HttpClient { BaseAddress = new Uri(options.TjekBaseUrl), Timeout = TimeSpan.FromSeconds(30) };
http.DefaultRequestHeaders.UserAgent.ParseAdd("Hylden/1.0");
var tjek = new TjekClient(http, Options.Create(options), loggerFactory.CreateLogger<TjekClient>());

var stores = await tjek.GetNearbyStoresAsync(CancellationToken.None);
if (stores.Count == 0)
{
    log.LogError("Fandt ingen butikker. Skriver ingenting.");
    return 1;
}
var all = await tjek.GetOffersAsync(stores.Select(s => s.DealerId), CancellationToken.None);

var now = DateTime.UtcNow;
// Behold kun mad og dagligvarer, og kun tilbud der ikke er udløbet. Fjerner dubletter.
var offers = all.Where(o => o.ValidTo >= now && Categorizer.IsGrocery(o.Heading))
    .DistinctBy(o => o.TjekId)
    .OrderBy(o => o.DealerName).ThenBy(o => o.Heading)
    .ToList();
if (offers.Count == 0)
{
    log.LogError("Fik 0 tilbud. Skriver ingenting, så gårsdagens data bevares.");
    return 1;
}

// Dagens snapshot: billigste pris pr. vare og kæde blandt de tilbud der gælder lige nu.
var snapshot = new DailySnapshot
{
    Date = LocalToday().ToString("yyyy-MM-dd"),
    Rows = offers.Where(o => o.ValidFrom <= now)
        .GroupBy(o => (o.ProductKey, o.DealerName))
        .Select(g => g.MinBy(o => o.Price)!)
        .Select(o => new SnapshotRow
        {
            ProductKey = o.ProductKey, Heading = o.Heading, DealerName = o.DealerName,
            Price = o.Price, UnitPrice = o.UnitPrice, UnitLabel = o.UnitLabel,
        })
        .OrderBy(r => r.DealerName).ThenBy(r => r.ProductKey)
        .ToList(),
};

Directory.CreateDirectory(Path.Combine(outDir, "snapshots"));
await File.WriteAllTextAsync(Path.Combine(outDir, "snapshots", snapshot.Date + ".json"),
    JsonSerializer.Serialize(snapshot, HyldenJson.Default.DailySnapshot));

var dates = Directory.GetFiles(Path.Combine(outDir, "snapshots"), "*.json")
    .Select(f => Path.GetFileNameWithoutExtension(f)).Order().ToList();

var data = new OfferData
{
    FetchedAt = now,
    Location = new DataLocation
    {
        Label = options.Label, Latitude = options.Latitude, Longitude = options.Longitude, RadiusMeters = options.RadiusMeters,
    },
    Stores = stores,
    Offers = offers,
    SnapshotDates = dates,
};
await File.WriteAllTextAsync(Path.Combine(outDir, "latest.json"),
    JsonSerializer.Serialize(data, HyldenJson.Default.OfferData));

log.LogInformation("Gemte {Stores} butikker, {Offers} tilbud og {Rows} snapshot-rækker i {Dir}",
    stores.Count, offers.Count, snapshot.Rows.Count, outDir);
return 0;

static DateOnly LocalToday()
{
    try { return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen"))); }
    catch (TimeZoneNotFoundException) { return DateOnly.FromDateTime(DateTime.UtcNow); }
}
