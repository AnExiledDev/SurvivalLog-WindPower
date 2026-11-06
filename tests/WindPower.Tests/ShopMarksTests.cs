using WindPower.Core;
using Xunit;

namespace WindPower.Tests;

// A shop keeps its own counts in the save, so a stock setting changed while the shop was not loaded has to reach it
// through the setting the shop last took, without handing back turbines the player already bought.
public class ShopMarksTests
{
    [Fact]
    public void ASettingRaisedWhileTheShopWasAwayAddsTheDifference()
    {
        // The shop took 2, the player bought one, then the setting went to 4.
        Assert.Equal(3, ShopMarks.Count(current: 1, applied: 2, listed: 4));
    }

    [Fact]
    public void ASettingLoweredTakesTheDifferenceOffButNeverBelowNone()
    {
        Assert.Equal(1, ShopMarks.Count(current: 2, applied: 2, listed: 1));
        Assert.Equal(0, ShopMarks.Count(current: 0, applied: 2, listed: 1));
    }

    [Fact]
    public void AnUnchangedSettingLeavesBoughtTurbinesBought()
    {
        Assert.Equal(0, ShopMarks.Count(current: 0, applied: 2, listed: 2));
    }

    [Fact]
    public void AShopFromBeforeTheMarksCanOnlyComeDownToTheSetting()
    {
        Assert.Equal(1, ShopMarks.Count(current: 2, applied: null, listed: 1));
        Assert.Equal(2, ShopMarks.Count(current: 2, applied: null, listed: 5));
    }

    [Fact]
    public void AShopWithoutTheTurbineGetsTheFullStockOrStillNone()
    {
        Assert.Equal(3, ShopMarks.Count(current: null, applied: 1, listed: 3));
        Assert.Null(ShopMarks.Count(current: null, applied: null, listed: 0));
    }

    // The game's sell-out event changes every count above 0, so a mark must never be one.
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(99)]
    public void AMarkIsNegativeAndReadsBack(int setting)
    {
        var mark = ShopMarks.Encode(setting);

        Assert.True(mark < 0);
        Assert.Equal(setting, ShopMarks.Decode(mark));
    }

    [Fact]
    public void ACountIsNotAMarkAndNoFurnitureSharesAMarkKey()
    {
        Assert.Null(ShopMarks.Decode(3));

        foreach (var kind in TurbineRows.All) Assert.True(ShopMarks.Key(kind.Ids.Furniture) < 0);
    }
}
