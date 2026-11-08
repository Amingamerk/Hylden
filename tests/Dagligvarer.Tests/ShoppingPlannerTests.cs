using Dagligvarer.Core;

namespace Dagligvarer.Tests;

public class ShoppingPlannerTests
{
    private static Offer O(string dealer, string heading, decimal price, decimal? pre = null) =>
        new() { DealerName = dealer, Heading = heading, Price = price, PrePrice = pre, ProductKey = TextKey.Normalize(heading) };

    private static ShoppingListItem I(int id, string q, int qty = 1) => new() { Id = id, Query = q, Quantity = qty };

    private readonly List<Offer> _offers =
    [
        O("Lidl", "Letmælk 1 l", 9.95m, 12m),
        O("Rema 1000", "Letmælk 1 l", 10.50m),
        O("Rema 1000", "Bananer", 2.00m),
        O("Føtex", "Bananer", 1.50m),
        O("Føtex", "Kaffe 500 g", 35m, 50m),
    ];

    [Fact]
    public void Picks_cheapest_store_per_item_when_unlimited()
    {
        var plan = ShoppingPlanner.Plan([I(1, "mælk"), I(2, "bananer"), I(3, "kaffe")], _offers, maxStores: 99);

        Assert.Equal(2, plan.Stops.Count);
        Assert.Equal(9.95m + 1.50m + 35m, plan.Total);
        Assert.Contains(plan.Stops, s => s.DealerName == "Lidl");
    }

    [Fact]
    public void One_store_limit_keeps_most_items_covered()
    {
        var plan = ShoppingPlanner.Plan([I(1, "mælk"), I(2, "bananer"), I(3, "kaffe")], _offers, maxStores: 1);

        // Rema og Føtex dækker begge 2 varer. Rema er billigst for de 2 (12,50 mod 36,50).
        var stop = Assert.Single(plan.Stops);
        Assert.Equal("Rema 1000", stop.DealerName);
        Assert.Single(plan.NotOnOffer);
    }

    [Fact]
    public void Quantity_multiplies_price_and_savings()
    {
        var plan = ShoppingPlanner.Plan([I(1, "kaffe", qty: 2)], _offers, maxStores: 2);

        Assert.Equal(70m, plan.Total);
        Assert.Equal(30m, plan.Saved);
    }

    [Fact]
    public void Items_without_offers_are_listed_separately()
    {
        var plan = ShoppingPlanner.Plan([I(1, "toiletpapir")], _offers, maxStores: 2);

        Assert.Empty(plan.Stops);
        Assert.Equal("toiletpapir", Assert.Single(plan.NotOnOffer).Query);
    }

    [Theory]
    [InlineData("Arla Letmælk 1 l", "mælk", true)]
    [InlineData("Arla Letmælk 1 l", "kaffe", false)]
    [InlineData("Arla Letmælk 1 l", "letmælk", true)]
    [InlineData("Hakket oksekød 8-12%", "oksekød hakket", true)]
    [InlineData("FØTEX Bananer", "bananer", true)]
    public void Text_matching_uses_all_words(string heading, string query, bool expected) =>
        Assert.Equal(expected, TextKey.Matches(heading, query));
}
