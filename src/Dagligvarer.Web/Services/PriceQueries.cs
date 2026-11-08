using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Dagligvarer.Core;
using Microsoft.AspNetCore.Components.WebAssembly.Http;
using Microsoft.JSInterop;

namespace Dagligvarer.Web.Services;

public record HistoryPoint(DateOnly Date, decimal Price);
public record HistorySeries(string DealerName, List<HistoryPoint> Points);

/// <summary>
/// Alt appen læser og gemmer. Tilbud og prishistorik hentes som statiske JSON-filer fra
/// data/ (skrevet af Dagligvarer.Fetcher), og indkøbslisten ligger i browserens localStorage.
/// Der er ingen server og ingen database.
/// </summary>
public class PriceQueries(HttpClient http, IJSRuntime js)
{
    private const string ListKey = "hylden.indkoebsliste";
    /// <summary>Hvor mange dages snapshots grafen indlæser.</summary>
    private const int MaxHistoryDays = 90;

    private Task<OfferData>? _data;
    private Task<List<DailySnapshot>>? _history;
    private List<ShoppingListItem>? _list;

    /// <summary>Tekst hvis data/latest.json ikke kunne hentes.</summary>
    public string? Error { get; private set; }

    public Task<OfferData> DataAsync() => _data ??= LoadDataAsync();

    /// <summary>Henter data/latest.json igen, uden om browserens cache.</summary>
    public Task<OfferData> ReloadAsync()
    {
        _data = null;
        _history = null;
        return DataAsync();
    }

    private async Task<OfferData> LoadDataAsync()
    {
        try
        {
            var data = await GetJsonAsync("data/latest.json", HyldenJson.Default.OfferData);
            Error = null;
            return data;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException)
        {
            Error = "Kunne ikke hente tilbudsdata. Prøv igen om lidt.";
            _data = null; // prøv igen næste gang
            return new OfferData();
        }
    }

    private async Task<T> GetJsonAsync<T>(string url, JsonTypeInfo<T> info)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        // Spørg serveren om filen er ændret, så man ikke ser gårsdagens priser fra browserens cache.
        req.SetBrowserRequestCache(BrowserRequestCache.NoCache);
        using var res = await http.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync(info))!;
    }

    public async Task<List<Offer>> CurrentOffersAsync()
    {
        var data = await DataAsync();
        var now = DateTime.UtcNow;
        return data.Offers.Where(o => o.ValidFrom <= now && o.ValidTo >= now).OrderBy(o => o.Heading).ToList();
    }

    public async Task<List<Store>> StoresAsync() =>
        (await DataAsync()).Stores.OrderBy(s => s.DistanceMeters).ToList();

    /// <summary>Hvornår data sidst blev hentet. Null hvis intet er hentet.</summary>
    public async Task<DateTime?> FetchedAtAsync()
    {
        var data = await DataAsync();
        return data.FetchedAt == default ? null : data.FetchedAt;
    }

    public async Task<DataLocation> LocationAsync() => (await DataAsync()).Location;

    /// <summary>Historik for én bestemt vare (samme navn), én linje pr. kæde.</summary>
    public async Task<List<HistorySeries>> HistoryForProductAsync(string productKey) =>
        ToSeries(await RowsAsync(r => r.ProductKey == productKey));

    /// <summary>Historik for et søgeord, fx "mælk": billigste match pr. kæde pr. dag.</summary>
    public async Task<List<HistorySeries>> HistoryForQueryAsync(string query) =>
        ToSeries(await RowsAsync(r => TextKey.Matches(r.Heading, query)));

    public async Task<string?> HeadingForAsync(string productKey)
    {
        var data = await DataAsync();
        if (data.Offers.FirstOrDefault(o => o.ProductKey == productKey) is { } offer) return offer.Heading;
        var rows = await RowsAsync(r => r.ProductKey == productKey);
        return rows.Count == 0 ? null : rows[^1].Row.Heading;
    }

    private async Task<List<(DateOnly Date, SnapshotRow Row)>> RowsAsync(Func<SnapshotRow, bool> filter)
    {
        _history ??= LoadHistoryAsync();
        var days = await _history;
        return days.SelectMany(d => d.Rows.Where(filter).Select(r => (DateOnly.Parse(d.Date), r))).ToList();
    }

    private async Task<List<DailySnapshot>> LoadHistoryAsync()
    {
        var dates = (await DataAsync()).SnapshotDates.TakeLast(MaxHistoryDays).ToList();
        var loaded = await Task.WhenAll(dates.Select(async date =>
        {
            try { return await GetJsonAsync($"data/snapshots/{date}.json", HyldenJson.Default.DailySnapshot); }
            catch (Exception ex) when (ex is HttpRequestException or JsonException) { return null; }
        }));
        return loaded.OfType<DailySnapshot>().ToList();
    }

    private static List<HistorySeries> ToSeries(List<(DateOnly Date, SnapshotRow Row)> rows) => rows
        .GroupBy(r => r.Row.DealerName)
        .Select(g => new HistorySeries(g.Key, g.GroupBy(r => r.Date)
            .Select(d => new HistoryPoint(d.Key, d.Min(r => r.Row.Price)))
            .OrderBy(p => p.Date).ToList()))
        .OrderBy(s => s.DealerName).ToList();

    // ---- Indkøbsliste (localStorage) ----

    public async Task<List<ShoppingListItem>> ShoppingListAsync()
    {
        var list = await LoadListAsync();
        return list.OrderBy(i => i.Done).ThenBy(i => i.AddedAt).ToList();
    }

    public async Task AddToListAsync(string query, int quantity = 1)
    {
        query = query.Trim();
        if (query == "") return;
        var list = await LoadListAsync();
        var existing = list.FirstOrDefault(i => !i.Done && TextKey.Normalize(i.Query) == TextKey.Normalize(query));
        if (existing is not null) existing.Quantity += quantity;
        else list.Add(new ShoppingListItem { Id = list.Count == 0 ? 1 : list.Max(i => i.Id) + 1, Query = query, Quantity = quantity });
        await SaveListAsync();
    }

    public async Task UpdateItemAsync(int id, Action<ShoppingListItem> change)
    {
        if ((await LoadListAsync()).FirstOrDefault(i => i.Id == id) is { } item) { change(item); await SaveListAsync(); }
    }

    public async Task RemoveItemAsync(int id)
    {
        (await LoadListAsync()).RemoveAll(i => i.Id == id);
        await SaveListAsync();
    }

    public async Task ClearDoneAsync()
    {
        (await LoadListAsync()).RemoveAll(i => i.Done);
        await SaveListAsync();
    }

    private async Task<List<ShoppingListItem>> LoadListAsync()
    {
        if (_list is not null) return _list;
        try
        {
            var json = await js.InvokeAsync<string?>("localStorage.getItem", ListKey);
            _list = string.IsNullOrEmpty(json) ? [] : JsonSerializer.Deserialize(json, HyldenJson.Default.ListShoppingListItem) ?? [];
        }
        catch (Exception ex) when (ex is JSException or JsonException) { _list = []; }
        return _list;
    }

    private async Task SaveListAsync()
    {
        try { await js.InvokeVoidAsync("localStorage.setItem", ListKey, JsonSerializer.Serialize(_list ?? [], HyldenJson.Default.ListShoppingListItem)); }
        catch (JSException) { /* privat browsing e.l.: listen virker stadig mens siden er åben */ }
    }
}
