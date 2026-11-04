using WindPower.Core;
using Xunit;

namespace WindPower.Tests;

public class SaveTwinTests
{
    private static SaveTwin.Fields Placed() => new() { AgentConfigId = TurbineRows.Ids.Furniture };

    // Without the mod the game must find an id it knows, or the save does not load.
    [Fact]
    public void APlacedTurbineIsSavedAsTheStockSolarPanel()
    {
        var save = Placed();

        Assert.True(SaveTwin.ToStock(ref save));
        Assert.Equal(TurbineRows.Stock.Furniture, save.AgentConfigId);
        Assert.StartsWith(SaveTwin.Marker, save.Name);
    }

    [Fact]
    public void LoadingWithTheModTurnsItBackIntoATurbine()
    {
        var save = Placed();

        SaveTwin.ToStock(ref save);

        Assert.True(SaveTwin.FromStock(ref save));
        Assert.Equal(TurbineRows.Ids.Furniture, save.AgentConfigId);
        Assert.Null(save.Name);
    }

    [Fact]
    public void APackedTurbineSwapsItsPackedIdToo()
    {
        var save = new SaveTwin.Fields { AgentConfigId = 7, IsBagFurniture = true, BagFurnitureConfigId = TurbineRows.Ids.Furniture };

        SaveTwin.ToStock(ref save);

        Assert.Equal((7, TurbineRows.Stock.Furniture), (save.AgentConfigId, save.BagFurnitureConfigId));

        SaveTwin.FromStock(ref save);

        Assert.Equal((7, TurbineRows.Ids.Furniture), (save.AgentConfigId, save.BagFurnitureConfigId));
    }

    // A real solar panel has no marker, so loading must never turn it into a turbine.
    [Fact]
    public void ARealSolarPanelStaysASolarPanel()
    {
        var save = new SaveTwin.Fields { AgentConfigId = TurbineRows.Stock.Furniture };

        Assert.False(SaveTwin.ToStock(ref save));
        Assert.False(SaveTwin.FromStock(ref save));
        Assert.Equal(TurbineRows.Stock.Furniture, save.AgentConfigId);
        Assert.Null(save.Name);
    }

    // Only the ids the marker names turn back: a marked packed id never changes the placed one.
    [Fact]
    public void OnlyTheIdsTheMarkerNamesAreTurnedBack()
    {
        var save = new SaveTwin.Fields { AgentConfigId = TurbineRows.Stock.Furniture, IsBagFurniture = true, BagFurnitureConfigId = TurbineRows.Stock.Furniture, Name = SaveTwin.Marker + "b" };

        SaveTwin.FromStock(ref save);

        Assert.Equal((TurbineRows.Stock.Furniture, TurbineRows.Ids.Furniture), (save.AgentConfigId, save.BagFurnitureConfigId));
    }

    [Fact]
    public void ASaveThatAlreadyHasANameIsLeftAlone()
    {
        var save = Placed();

        save.Name = "Kitchen";

        Assert.False(SaveTwin.ToStock(ref save));
        Assert.Equal((TurbineRows.Ids.Furniture, "Kitchen"), (save.AgentConfigId, save.Name));
    }

    [Fact]
    public void OtherFurnitureIsNotTouched()
    {
        var save = new SaveTwin.Fields { AgentConfigId = 40001 };

        Assert.False(SaveTwin.ToStock(ref save));
        Assert.Equal(40001, save.AgentConfigId);
        Assert.Null(save.Name);
    }
}
