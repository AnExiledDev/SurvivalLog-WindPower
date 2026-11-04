using WindPower.Core;
using Xunit;

namespace WindPower.Tests;

public class UnlocksTests
{
    [Fact]
    public void AQuestRewardGainsTheTurbinesRecipeAtTheEnd()
    {
        Assert.Equal(new[] { 309, 310, 9102 }, Unlocks.WithRecipes(new[] { 309, 310 }, new[] { 9102 }));
    }

    [Fact]
    public void ARewardThatAlreadyHasTheRecipeIsLeftAlone()
    {
        Assert.Null(Unlocks.WithRecipes(new[] { 309, 9102 }, new[] { 9102 }));
    }

    [Fact]
    public void OnlyTheMissingRecipesAreAddedAndEachOnce()
    {
        Assert.Equal(new[] { 9101, 9102 }, Unlocks.WithRecipes(new[] { 9101 }, new[] { 9101, 9102, 9102 }));
    }

    private static readonly int[] Ours = { 910001, 910002 };

    [Fact]
    public void AShopGainsEachTurbineWithItsStockAndKeepsItsOwnGoods()
    {
        var shop = Unlocks.Shop(new[] { 40001, 40002 }, new[] { 3, 1 }, Ours, new[] { (910002, 2), (910001, 2) });

        Assert.Equal(new[] { 40001, 40002, 910002, 910001 }, shop!.Value.Ids);
        Assert.Equal(new[] { 3, 1, 2, 2 }, shop.Value.Counts);
    }

    [Fact]
    public void AShopAlreadyStockedAsSetIsLeftAlone()
    {
        Assert.Null(Unlocks.Shop(new[] { 40001, 910002 }, new[] { 3, 2 }, Ours, new[] { (910002, 2) }));
    }

    [Fact]
    public void ANewStockSettingReplacesTheTurbinesCount()
    {
        var shop = Unlocks.Shop(new[] { 910002, 40001 }, new[] { 2, 3 }, Ours, new[] { (910002, 5) });

        Assert.Equal(new[] { 40001, 910002 }, shop!.Value.Ids);
        Assert.Equal(new[] { 3, 5 }, shop.Value.Counts);
    }

    [Fact]
    public void AStockOfNoneTakesTheTurbineOffTheShelves()
    {
        var shop = Unlocks.Shop(new[] { 40001, 910002 }, new[] { 3, 2 }, Ours, new[] { (910002, 0) });

        Assert.Equal(new[] { 40001 }, shop!.Value.Ids);
        Assert.Equal(new[] { 3 }, shop.Value.Counts);
    }

    [Fact]
    public void TheQuestTextGainsTheWindLineAsItsOwnParagraph()
    {
        Assert.Equal("Build panels.\n\nWind too.", Unlocks.WithLine("Build panels.", "Wind too.", "\n\n"));
    }

    [Fact]
    public void TheWindLineIsNeverAddedTwice()
    {
        Assert.Null(Unlocks.WithLine("Build panels.\\n\\nWind too.", "Wind too.", "\\n\\n"));
    }

    [Fact]
    public void AnEmptyQuestTextBecomesJustTheLine()
    {
        Assert.Equal("Wind too.", Unlocks.WithLine(null, "Wind too.", "\n\n"));
    }

    // A live shop moves by the change in the setting, so turbines already bought stay bought.
    [Theory]
    [InlineData(2, 2, 3, 3)]   // nothing bought: the shop fills to the new setting
    [InlineData(1, 2, 3, 2)]   // one bought: one more than before
    [InlineData(2, 2, 0, 0)]   // off the shelves
    [InlineData(0, 2, 1, 0)]   // sold out and lowered: never below none
    [InlineData(5, 2, 3, 3)]   // more than the setting somehow: never above a full shop
    public void ALiveShopsCountMovesByTheChangeInTheSetting(int current, int before, int after, int expected)
    {
        Assert.Equal(expected, Unlocks.NewStock(current, before, after));
    }
}
