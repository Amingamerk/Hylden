
namespace Dagligvarer.Core;

public record PlanLine(ShoppingListItem Item, Offer? Offer, decimal? LineTotal);
public record StoreStop(string DealerName, List<PlanLine> Lines, decimal Total);
public record ShoppingPlan(List<StoreStop> Stops, List<ShoppingListItem> NotOnOffer, decimal Total, decimal Saved);

/// <summary>
/// Finder hvor hver vare på listen er billigst. Med maxStores kan man begrænse hvor
/// mange butikker man vil rundt i. Der prøves alle kombinationer af kæder
/// (med 5 kæder er det kun 31 kombinationer), og den billigste vælges.
/// </summary>
public static class ShoppingPlanner
{
    public static ShoppingPlan Plan(IReadOnlyList<ShoppingListItem> items, IReadOnlyList<Offer> offers, int maxStores)
    {
        var open = items.Where(i => !i.Done).ToList();

        // Alle tilbud der matcher hver vare, billigste pr. kæde.
        var matches = open.ToDictionary(i => i.Id, i => offers
            .Where(o => TextKey.Matches(o.Heading, i.Query))
            .GroupBy(o => o.DealerName)
            .ToDictionary(g => g.Key, g => g.MinBy(o => o.Price)!));

        // Kæder sorteret efter hvor mange varer de har på tilbud. Max 12, så kombinationerne holdes små.
        var dealers = matches.Values.SelectMany(m => m.Keys)
            .GroupBy(d => d).OrderByDescending(g => g.Count()).ThenBy(g => g.Key)
            .Select(g => g.Key).Take(12).ToList();
        maxStores = Math.Clamp(maxStores, 1, Math.Max(1, dealers.Count));

        IReadOnlyCollection<string> bestSet = [];
        (int covered, decimal cost) bestScore = (0, 0);
        foreach (var set in Subsets(dealers, maxStores))
        {
            var score = Score(open, matches, set);
            // Flest varer dækket først, derefter lavest pris, derefter færrest butikker.
            if (score.covered > bestScore.covered ||
                (score.covered == bestScore.covered && score.cost < bestScore.cost) ||
                (score.covered == bestScore.covered && score.cost == bestScore.cost && set.Count < bestSet.Count))
            {
                bestScore = score;
                bestSet = set;
            }
        }

        var lines = new List<PlanLine>();
        var missing = new List<ShoppingListItem>();
        foreach (var item in open)
        {
            var offer = Cheapest(matches[item.Id], bestSet);
            if (offer is null) missing.Add(item);
            else lines.Add(new PlanLine(item, offer, offer.Price * item.Quantity));
        }

        var stops = lines.GroupBy(l => l.Offer!.DealerName)
            .Select(g => new StoreStop(g.Key, g.ToList(), g.Sum(l => l.LineTotal!.Value)))
            .OrderByDescending(s => s.Total).ToList();
        var saved = lines.Sum(l => l.Offer!.PrePrice is decimal pre && pre > l.Offer.Price
            ? (pre - l.Offer.Price) * l.Item.Quantity : 0);
        return new ShoppingPlan(stops, missing, stops.Sum(s => s.Total), saved);
    }

    private static (int covered, decimal cost) Score(List<ShoppingListItem> items,
        Dictionary<int, Dictionary<string, Offer>> matches, IReadOnlyCollection<string> set)
    {
        int covered = 0; decimal cost = 0;
        foreach (var item in items)
        {
            if (Cheapest(matches[item.Id], set) is { } o) { covered++; cost += o.Price * item.Quantity; }
        }
        return (covered, cost);
    }

    private static Offer? Cheapest(Dictionary<string, Offer> byDealer, IReadOnlyCollection<string> set) =>
        byDealer.Where(kv => set.Contains(kv.Key)).Select(kv => kv.Value).MinBy(o => o.Price);

    private static IEnumerable<IReadOnlyCollection<string>> Subsets(List<string> items, int maxSize)
    {
        var n = items.Count;
        for (var mask = 1; mask < (1 << n); mask++)
        {
            if (System.Numerics.BitOperations.PopCount((uint)mask) > maxSize) continue;
            yield return Enumerable.Range(0, n).Where(i => (mask & (1 << i)) != 0).Select(i => items[i]).ToList();
        }
    }
}
