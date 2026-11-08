using Dagligvarer.Core;


namespace Dagligvarer.Tests;

public class CategorizerTests
{
    [Theory]
    [InlineData("Vaskemaskine 8 kg", false)]
    [InlineData("Samsung TV 55 tommer", false)]
    [InlineData("Herrejakke", false)]
    [InlineData("Havemøbler", false)]
    [InlineData("Pandekager 4 stk", true)]
    [InlineData("Marmeladeglas 400 g", true)]
    [InlineData("Pærer 1 kg", true)]
    [InlineData("Toiletpapir 8 ruller", true)]
    public void IsGrocery_filters_non_food(string heading, bool expected) =>
        Assert.Equal(expected, Categorizer.IsGrocery(heading));

    [Theory]
    [InlineData("Letmælk 1 l", "Mejeri og æg")]
    [InlineData("Mælkechokolade", "Snacks og slik")]
    [InlineData("Hakket oksekød 8-12%", "Kød og pålæg")]
    [InlineData("Kyllingebryst", "Kød og pålæg")]
    [InlineData("Bananer", "Frugt og grønt")]
    [InlineData("Rugbrød", "Brød og kager")]
    [InlineData("Coca-Cola 1,5 l", "Drikkevarer")]
    [InlineData("Toiletpapir", "Husholdning og pleje")]
    [InlineData("Noget mærkeligt", "Øvrigt")]
    public void For_picks_category(string heading, string expected) =>
        Assert.Equal(expected, Categorizer.For(heading));
}
