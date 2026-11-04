using WindPower.Core;
using Xunit;

namespace WindPower.Tests;

// A game without the mod drops what it does not know, so Uninstall prep must turn every turbine id into the solar
// panel of the same size, and leave everything else alone.
public class UninstallTests
{
    [Theory]
    [InlineData("Small", 40000)]
    [InlineData("Medium", 40001)]
    [InlineData("Large", 40002)]
    public void EachTurbineBecomesTheSolarPanelOfItsSize(string size, int solarPanel)
    {
        var kind = TurbineRows.All.Single(kind => kind.Size == size);

        Assert.Equal(solarPanel, Uninstall.StockFurniture(kind.Ids.Furniture, TurbineRows.All));
        Assert.Equal(kind.Stock.Item, Uninstall.StockItem(kind.Ids.Item, TurbineRows.All));
        Assert.NotEqual(kind.Ids.Item, kind.Stock.Item);
    }

    [Theory]
    [InlineData(40000)]
    [InlineData(20004)]
    [InlineData(0)]
    public void AnythingElseIsLeftAlone(int id)
    {
        Assert.Null(Uninstall.StockItem(id, TurbineRows.All));
        Assert.Null(Uninstall.StockFurniture(id, TurbineRows.All));
    }

    [Fact]
    public void BoughtTurbinesAreRecordedAsTheirSolarPanels()
    {
        var records = new Dictionary<int, int>
        {
            [40001] = 4,
            [TurbineRows.Medium.Ids.Furniture] = 2,
            [TurbineRows.Large.Ids.Furniture] = 0,
            [TurbineRows.Small.Ids.Furniture] = 1,
        };

        var moves = Uninstall.LedgerMoves(records, TurbineRows.All);

        Assert.Equal(new[] { (TurbineRows.Medium.Ids.Furniture, 40001, 2), (TurbineRows.Small.Ids.Furniture, 40000, 1) }, moves);
    }
}
