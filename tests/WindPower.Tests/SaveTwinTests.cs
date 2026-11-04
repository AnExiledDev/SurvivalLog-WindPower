using WindPower.Core;
using Xunit;

namespace WindPower.Tests;

public class SaveTwinTests
{
    private static readonly IReadOnlyList<TurbineKind> Kinds = TurbineRows.All;

    private static TurbineKind Kind(string size) => TurbineRows.All.Single(kind => kind.Size == size);

    private static SaveTwin.Fields Placed(TurbineKind kind) => new() { AgentConfigId = kind.Ids.Furniture };

    // Without the mod the game must find an id it knows, or the save does not load.
    [Theory]
    [InlineData("Medium", 40001)]
    [InlineData("Large", 40002)]
    public void APlacedTurbineIsSavedAsTheSolarPanelOfItsSize(string size, int solarPanel)
    {
        var kind = Kind(size);
        var save = Placed(kind);

        Assert.True(SaveTwin.ToStock(ref save, Kinds));
        Assert.Equal(solarPanel, save.AgentConfigId);
        Assert.StartsWith(kind.SaveMarker, save.Name);
        Assert.StartsWith(SaveTwin.MarkerStart, save.Name);
    }

    [Theory]
    [InlineData("Medium")]
    [InlineData("Large")]
    public void LoadingWithTheModTurnsItBackIntoTheSameTurbine(string size)
    {
        var kind = Kind(size);
        var save = Placed(kind);

        SaveTwin.ToStock(ref save, Kinds);

        Assert.True(SaveTwin.FromStock(ref save, Kinds));
        Assert.Equal(kind.Ids.Furniture, save.AgentConfigId);
        Assert.Null(save.Name);
    }

    [Theory]
    [InlineData("Medium")]
    [InlineData("Large")]
    public void APackedTurbineSwapsItsPackedIdToo(string size)
    {
        var kind = Kind(size);
        var save = new SaveTwin.Fields { AgentConfigId = 7, IsBagFurniture = true, BagFurnitureConfigId = kind.Ids.Furniture };

        SaveTwin.ToStock(ref save, Kinds);

        Assert.Equal((7, kind.Stock.Furniture), (save.AgentConfigId, save.BagFurnitureConfigId));

        SaveTwin.FromStock(ref save, Kinds);

        Assert.Equal((7, kind.Ids.Furniture), (save.AgentConfigId, save.BagFurnitureConfigId));
    }

    // Saves from before the medium turbine carry this exact marker, and must still load as large turbines.
    [Fact]
    public void AnOlderSavesTurbineLoadsAsTheLargeTurbine()
    {
        var save = new SaveTwin.Fields { AgentConfigId = 40002, Name = "WindPower.Turbine:a" };

        Assert.True(SaveTwin.FromStock(ref save, Kinds));
        Assert.Equal(TurbineRows.Large.Ids.Furniture, save.AgentConfigId);
        Assert.Null(save.Name);
    }

    // The large marker is the start of the medium's text up to "Turbine", so a loose match would read one as the other.
    [Fact]
    public void AMediumTurbinesMarkerIsNeverReadAsALargeOne()
    {
        foreach (var kinds in new[] { Kinds, Kinds.Reverse().ToList() })
        {
            var save = new SaveTwin.Fields { AgentConfigId = TurbineRows.Medium.Stock.Furniture, Name = TurbineRows.Medium.SaveMarker + "a" };

            Assert.True(SaveTwin.FromStock(ref save, kinds));
            Assert.Equal(TurbineRows.Medium.Ids.Furniture, save.AgentConfigId);
        }
    }

    // A real solar panel has no marker, so loading must never turn it into a turbine.
    [Theory]
    [InlineData(40001)]
    [InlineData(40002)]
    public void ARealSolarPanelStaysASolarPanel(int solarPanel)
    {
        var save = new SaveTwin.Fields { AgentConfigId = solarPanel };

        Assert.False(SaveTwin.ToStock(ref save, Kinds));
        Assert.False(SaveTwin.FromStock(ref save, Kinds));
        Assert.Equal(solarPanel, save.AgentConfigId);
        Assert.Null(save.Name);
    }

    // Only the ids the marker names turn back: a marked packed id never changes the placed one.
    [Fact]
    public void OnlyTheIdsTheMarkerNamesAreTurnedBack()
    {
        var kind = TurbineRows.Large;
        var save = new SaveTwin.Fields { AgentConfigId = kind.Stock.Furniture, IsBagFurniture = true, BagFurnitureConfigId = kind.Stock.Furniture, Name = kind.SaveMarker + "b" };

        SaveTwin.FromStock(ref save, Kinds);

        Assert.Equal((kind.Stock.Furniture, kind.Ids.Furniture), (save.AgentConfigId, save.BagFurnitureConfigId));
    }

    [Fact]
    public void ASaveThatAlreadyHasANameIsLeftAlone()
    {
        var save = Placed(TurbineRows.Medium);

        save.Name = "Kitchen";

        Assert.False(SaveTwin.ToStock(ref save, Kinds));
        Assert.Equal((TurbineRows.Medium.Ids.Furniture, "Kitchen"), (save.AgentConfigId, save.Name));
    }

    [Fact]
    public void OtherFurnitureIsNotTouched()
    {
        var save = new SaveTwin.Fields { AgentConfigId = 40003, Name = "Kitchen" };

        Assert.False(SaveTwin.FromStock(ref save, Kinds));

        save.Name = null;

        Assert.False(SaveTwin.ToStock(ref save, Kinds));
        Assert.Equal(40003, save.AgentConfigId);
        Assert.Null(save.Name);
    }
}
