using WindPower.Core;
using Xunit;

namespace WindPower.Tests;

/// <summary>
/// The turbines' materials and returns are settled once for the table rows and the Mod Manager's questions alike, so
/// what the Mods screen says about a list is what the game then does with it.
/// </summary>
public class TurbineChoicesTests
{
    private static bool AnyItem(int id) => true;

    private static int? NoStockRecipe(IReadOnlyList<int> materials) => null;

    /// <summary>The settings in the order of TurbineRows.All (Medium, Large, Small), each from its defaults with changes.</summary>
    private static List<TurbineSettings> Settings(Func<TurbineKind, TurbineSettings, TurbineSettings>? change = null) =>
        TurbineRows.All.Select(kind => change == null ? kind.Defaults : change(kind, kind.Defaults)).ToList();

    [Fact]
    public void WithTheDefaultsEveryListCanBeUsedAndABlankReturnsGivesBackHalf()
    {
        var choices = TurbineChoices.Of(Settings(), AnyItem, NoStockRecipe);

        Assert.All(new[] { "MediumTurbine", "LargeTurbine", "SmallTurbine" }, section =>
        {
            Assert.Equal("", TurbineChoices.Problem(choices, section, "Materials"));
            Assert.Equal("", TurbineChoices.Problem(choices, section, "Returns"));
        });
        Assert.Equal("20004:3,20104:2,20210:1", TurbineChoices.Blank(choices, "LargeTurbine", "Returns"));
        Assert.Equal(MaterialList.Standard("20004:3, 20104:2, 20210:1"), choices[1].Back);
        Assert.Equal("Follows Return percent: 50% of the materials.\n按「Return percent」返还材料的 50%。", TurbineChoices.BlankNote(choices, "LargeTurbine", "Returns"));
    }

    [Fact]
    public void TheBlankReturnsFollowsTheReturnPercent()
    {
        var choices = TurbineChoices.Of(Settings((kind, s) => s with { ReturnPercent = 100 }), AnyItem, NoStockRecipe);

        Assert.Equal("20004:5,20104:3,20210:3,20373:1", TurbineChoices.Blank(choices, "LargeTurbine", "Returns"));
        Assert.StartsWith("Follows Return percent: 100%", TurbineChoices.BlankNote(choices, "LargeTurbine", "Returns"));
    }

    // Large comes after Medium, so Medium's list is taken; Medium comes before Small, so Small's default is kept for it.
    [Fact]
    public void ATurbineCannotTakeAnotherTurbinesMaterialsAndTheScreenSaysWhich()
    {
        var choices = TurbineChoices.Of(Settings((kind, s) => kind == TurbineRows.Large ? s with { Materials = "20104:2, 20004:4, 20210:2" } : kind == TurbineRows.Medium ? s with { Materials = "20004:2" } : s), AnyItem, NoStockRecipe);
        var clash = TurbineChoices.Of(Settings((kind, s) => kind == TurbineRows.Medium ? s with { Materials = TurbineRows.Small.Defaults.Materials } : s), AnyItem, NoStockRecipe);

        Assert.Equal("", TurbineChoices.Problem(choices, "MediumTurbine", "Materials"));
        Assert.Equal("", TurbineChoices.Problem(choices, "LargeTurbine", "Materials"));
        Assert.StartsWith("These are the Small Wind Turbine's default materials", TurbineChoices.Problem(clash, "MediumTurbine", "Materials"));
        Assert.Equal(MaterialList.Standard(TurbineRows.Medium.Defaults.Materials), clash[0].Materials);
    }

    // The turbine just before Small keeps Small's default free too, not only the first one.
    [Fact]
    public void TheLastTurbinesDefaultIsKeptFromTheOneJustBeforeIt()
    {
        var clash = TurbineChoices.Of(Settings((kind, s) => kind == TurbineRows.Large ? s with { Materials = TurbineRows.Small.Defaults.Materials } : s), AnyItem, NoStockRecipe);

        Assert.StartsWith("These are the Small Wind Turbine's default materials", TurbineChoices.Problem(clash, "LargeTurbine", "Materials"));
    }

    [Fact]
    public void ATurbineThatTakesTheSameMaterialsAsAnEarlierOneIsToldWhich()
    {
        var choices = TurbineChoices.Of(Settings((kind, s) => kind == TurbineRows.Large ? s with { Materials = "20104:2, 20004:4, 20210:2" } : s), AnyItem, NoStockRecipe);

        Assert.StartsWith("The Medium Wind Turbine already takes exactly these materials.", TurbineChoices.Problem(choices, "LargeTurbine", "Materials"));
    }

    // What a blank Returns gives back follows the materials the turbine really uses, its default when its own fail.
    [Fact]
    public void ABlankReturnsFollowsTheMaterialsInUse()
    {
        var choices = TurbineChoices.Of(Settings((kind, s) => kind == TurbineRows.Small ? s with { Materials = "20312:4" } : kind == TurbineRows.Large ? s with { Materials = "99999:2" } : s), id => id != 99999, NoStockRecipe);

        Assert.Equal("20312:2", TurbineChoices.Blank(choices, "SmallTurbine", "Returns"));
        Assert.Equal("20004:3,20104:2,20210:1", TurbineChoices.Blank(choices, "LargeTurbine", "Returns"));
        Assert.StartsWith("The game has no item 99999,", TurbineChoices.Problem(choices, "LargeTurbine", "Materials"));
    }

    [Fact]
    public void AReturnsListSetByHandIsWhatComesBack()
    {
        var choices = TurbineChoices.Of(Settings((kind, s) => kind == TurbineRows.Small ? s with { Returns = "20312:2" } : kind == TurbineRows.Medium ? s with { Returns = "none" } : s), AnyItem, NoStockRecipe);

        Assert.Equal(new[] { 20312, 20312 }, choices[2].Back);
        Assert.Empty(choices[0].Back);
        Assert.Equal("", TurbineChoices.Problem(choices, "SmallTurbine", "Returns"));
        Assert.Equal("20004:2", TurbineChoices.Blank(choices, "SmallTurbine", "Returns"));
    }

    [Fact]
    public void AStockRecipesMaterialsAreRefusedWithWhatItMakes()
    {
        var stock = MaterialList.Standard("20004:4");
        var choices = TurbineChoices.Of(Settings((kind, s) => kind == TurbineRows.Large ? s with { Materials = "20004:4" } : s), AnyItem, list => TurbineRows.MaterialKey(list) == TurbineRows.MaterialKey(stock) ? 309 : null, id => id == 309 ? ("Medium Solar Panel Package", "中型太阳能发电板包裹") : null);

        Assert.StartsWith("The game's own Medium Solar Panel Package recipe takes exactly these materials.", TurbineChoices.Problem(choices, "LargeTurbine", "Materials"));
    }

    [Fact]
    public void OtherSettingsGetNoAnswer()
    {
        var choices = TurbineChoices.Of(Settings((kind, s) => s with { Materials = "" }), AnyItem, NoStockRecipe);

        Assert.Equal("", TurbineChoices.Problem(choices, "LargeTurbine", "RatedPower"));
        Assert.Equal("", TurbineChoices.Problem(choices, "Dismantle", "ReturnPercent"));
        Assert.Equal("", TurbineChoices.Blank(choices, "LargeTurbine", "Materials"));
        Assert.Equal("", TurbineChoices.BlankNote(choices, "LargeTurbine", "Materials"));
        Assert.NotEqual("", TurbineChoices.Problem(choices, "LargeTurbine", "Materials"));
    }
}
