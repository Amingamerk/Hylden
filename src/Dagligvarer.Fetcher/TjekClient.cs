using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dagligvarer.Core;

/// <summary>
/// Klient til Tjek (eTilbudsavis) squid API v2. API'et er ikke officielt dokumenteret,
/// så JSON læses forsigtigt: mangler et felt, springes det over i stedet for at fejle.
/// </summary>
public class TjekClient(HttpClient http, IOptions<AppOptions> options, ILogger<TjekClient> log)
{
    private readonly AppOptions _opt = options.Value;
    private const int PageSize = 100;
    /// <summary>Tjek svarer 400 ved offset 1000 og derover.</summary>
    private const int MaxOffset = 1000;

    /// <summary>Størst søgeradius når en kæde ikke har nogen butik inden for den normale radius.</summary>
    private const int FallbackRadiusMeters = 10000;

    public async Task<List<Store>> GetNearbyStoresAsync(CancellationToken ct)
    {
        var stores = await FetchStoresAsync(_opt.RadiusMeters, ct, limitToRadius: true);
        if (_opt.Dealers.Count == 0) return stores;

        bool Matches(Store s, string d) => TextKey.Normalize(s.DealerName).Contains(TextKey.Normalize(d));
        stores = stores.Where(s => _opt.Dealers.Any(d => Matches(s, d))).ToList();

        // Kæder uden butik i radius (typisk Normal) bliver ellers helt udeladt.
        // Find den nærmeste butik længere væk, så kædens tilbud stadig hentes.
        var missing = _opt.Dealers.Where(d => !stores.Any(s => Matches(s, d))).ToList();
        if (missing.Count > 0)
        {
            log.LogWarning("Ingen butik inden for {Radius} m for: {Dealers}. Prøver op til {Fallback} m.",
                _opt.RadiusMeters, string.Join(", ", missing), FallbackRadiusMeters);
            var wide = await FetchStoresAsync(FallbackRadiusMeters, ct, limitToRadius: false);
            foreach (var d in missing)
            {
                var nearest = wide.Where(s => Matches(s, d)).OrderBy(s => s.DistanceMeters).FirstOrDefault();
                if (nearest is null)
                {
                    log.LogWarning("Fandt ingen {Dealer}-butik inden for {Fallback} m. Kæden findes måske ikke i Tjek under det navn.", d, FallbackRadiusMeters);
                    continue;
                }
                log.LogInformation("Bruger nærmeste {Dealer}: {Street}, {City} ({Dist} m væk)", d, nearest.Street, nearest.City, nearest.DistanceMeters);
                stores.Add(nearest);
            }
        }
        return stores;
    }

    private async Task<List<Store>> FetchStoresAsync(int radiusMeters, CancellationToken ct, bool limitToRadius)
    {
        var url = $"stores?r_lat={F(_opt.Latitude)}&r_lng={F(_opt.Longitude)}&r_radius={radiusMeters}&limit={PageSize}&order_by=distance";
        using var doc = await GetJsonAsync(url, ct);
        var stores = new List<Store>();
        foreach (var s in doc.RootElement.EnumerateArray())
        {
            var store = new Store
            {
                Id = Str(s, "id") ?? "",
                DealerId = Str(s, "dealer_id") ?? "",
                DealerName = Str(s, "branding", "name") ?? Str(s, "dealer", "name") ?? "Ukendt",
                Street = Str(s, "street") ?? "",
                City = Str(s, "city") ?? "",
                Latitude = Num(s, "latitude") ?? 0,
                Longitude = Num(s, "longitude") ?? 0,
                BrandColor = Color(Str(s, "branding", "color")),
            };
            store.DistanceMeters = (int)Math.Round(Geo.DistanceMeters(_opt.Latitude, _opt.Longitude, store.Latitude, store.Longitude));
            if (store.Id != "" && (!limitToRadius || store.DistanceMeters <= radiusMeters)) stores.Add(store);
        }
        return stores;
    }

    /// <summary>
    /// Henter tilbud én kæde ad gangen. Tjek tillader højst offset 1000 pr. forespørgsel,
    /// så ved at dele op pr. kæde får vi op til 1000 tilbud fra hver kæde.
    /// </summary>
    public async Task<List<Offer>> GetOffersAsync(IEnumerable<string> dealerIds, CancellationToken ct)
    {
        var result = new List<Offer>();
        foreach (var dealerId in dealerIds.Where(d => d != "").Distinct())
        {
            var offers = await GetOffersForDealerAsync(dealerId, withLocation: true, ct);
            if (offers.Count == 0)
            {
                // Ingen tilbud i radius: prøv landsdækkende tilbud for kæden (Normal har samme avis overalt).
                log.LogWarning("Ingen tilbud for kæde {Dealer} i radius, prøver uden lokation.", dealerId);
                offers = await GetOffersForDealerAsync(dealerId, withLocation: false, ct);
            }
            log.LogInformation("Kæde {Dealer}: {Count} tilbud hentet", dealerId, offers.Count);
            result.AddRange(offers);
        }
        return result;
    }

    private async Task<List<Offer>> GetOffersForDealerAsync(string dealerId, bool withLocation, CancellationToken ct)
    {
        var result = new List<Offer>();
        for (var offset = 0; offset < MaxOffset; offset += PageSize)
        {
            var loc = withLocation ? $"&r_lat={F(_opt.Latitude)}&r_lng={F(_opt.Longitude)}&r_radius={_opt.RadiusMeters}" : "";
            var url = $"offers?dealer_ids={dealerId}{loc}&limit={PageSize}&offset={offset}";
            JsonDocument doc;
            try
            {
                doc = await GetJsonAsync(url, ct);
            }
            catch (HttpRequestException ex) when (offset > 0)
            {
                // Senere sider kan afvises (fx over Tjeks grænse). Behold det vi har.
                log.LogInformation("Stopper ved offset {Offset} for kæde {Dealer}: {Message}", offset, dealerId, ex.Message);
                break;
            }
            using (doc)
            {
                result.AddRange(doc.RootElement.EnumerateArray().Select(ParseOffer).OfType<Offer>());
                if (doc.RootElement.GetArrayLength() < PageSize) break;
            }
        }
        return result;
    }

    private Offer? ParseOffer(JsonElement o)
    {
        var price = Num(o, "pricing", "price");
        var heading = Str(o, "heading");
        if (price is null || string.IsNullOrWhiteSpace(heading)) return null;

        var offer = new Offer
        {
            TjekId = Str(o, "id") ?? Guid.NewGuid().ToString(),
            DealerId = Str(o, "dealer_id") ?? "",
            DealerName = Str(o, "branding", "name") ?? Str(o, "dealer", "name") ?? "Ukendt",
            Heading = heading.Trim(),
            Description = Str(o, "description"),
            Price = (decimal)price.Value,
            PrePrice = Num(o, "pricing", "pre_price") is double pre ? (decimal)pre : null,
            ValidFrom = Date(Str(o, "run_from")) ?? DateTime.UtcNow,
            ValidTo = Date(Str(o, "run_till")) ?? DateTime.UtcNow.AddDays(7),
            ImageUrl = Str(o, "images", "view") ?? Str(o, "images", "thumb"),
            ProductKey = TextKey.Normalize(heading),
            FetchedAt = DateTime.UtcNow,
        };
        ApplyQuantity(o, offer);
        return offer;
    }

    /// <summary>Regner pris pr. kg / liter / stk. ud fra "quantity"-feltet.</summary>
    private static void ApplyQuantity(JsonElement o, Offer offer)
    {
        var from = Num(o, "quantity", "size", "from");
        var factor = Num(o, "quantity", "unit", "si", "factor");
        var siUnit = Str(o, "quantity", "unit", "si", "symbol");
        var unit = Str(o, "quantity", "unit", "symbol");
        var pieces = Num(o, "quantity", "pieces", "from") ?? 1;

        if (from is > 0 && unit is not null)
            offer.QuantityText = pieces > 1 ? $"{pieces:0} x {from:0.##} {unit}" : $"{from:0.##} {unit}";
        else if (pieces > 1)
            offer.QuantityText = $"{pieces:0} stk.";

        if (from is > 0 && factor is > 0 && siUnit is "kg" or "l")
        {
            var amount = from.Value * factor.Value * pieces;
            offer.UnitPrice = Math.Round(offer.Price / (decimal)amount, 2);
            offer.UnitLabel = siUnit == "kg" ? "kg" : "l";
        }
        else if (pieces > 1)
        {
            offer.UnitPrice = Math.Round(offer.Price / (decimal)pieces, 2);
            offer.UnitLabel = "stk.";
        }
    }

    private async Task<JsonDocument> GetJsonAsync(string relativeUrl, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, relativeUrl);
        if (!string.IsNullOrWhiteSpace(_opt.TjekApiKey)) req.Headers.Add("X-Api-Key", _opt.TjekApiKey);
        log.LogDebug("Tjek GET {Url}", relativeUrl);
        using var res = await http.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode)
            throw new HttpRequestException($"Tjek svarede {(int)res.StatusCode} på {relativeUrl}");
        await using var stream = await res.Content.ReadAsStreamAsync(ct);
        var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException($"Uventet svar fra Tjek på {relativeUrl}");
        return doc;
    }

    private static string F(double d) => d.ToString(CultureInfo.InvariantCulture);

    private static JsonElement? Path(JsonElement e, string[] path)
    {
        foreach (var p in path)
        {
            if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(p, out e)) return null;
        }
        return e;
    }

    private static string? Str(JsonElement e, params string[] path) => Path(e, path) is { } v
        ? v.ValueKind switch { JsonValueKind.String => v.GetString(), JsonValueKind.Number => v.GetRawText(), _ => null }
        : null;

    private static double? Num(JsonElement e, params string[] path) => Path(e, path) is { } v
        ? v.ValueKind switch
        {
            JsonValueKind.Number => v.GetDouble(),
            JsonValueKind.String when double.TryParse(v.GetString(), CultureInfo.InvariantCulture, out var d) => d,
            _ => null
        }
        : null;

    private static DateTime? Date(string? s) =>
        DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var d) ? d : null;

    private static string Color(string? c) =>
        string.IsNullOrWhiteSpace(c) ? "#333333" : c.StartsWith('#') ? c : "#" + c;
}

public static class Geo
{
    public static double DistanceMeters(double lat1, double lng1, double lat2, double lng2)
    {
        const double R = 6371000;
        double ToRad(double d) => d * Math.PI / 180;
        var dLat = ToRad(lat2 - lat1);
        var dLng = ToRad(lng2 - lng1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
}
