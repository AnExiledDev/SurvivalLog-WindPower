using WindPower.Core;
using Xunit;

namespace WindPower.Tests;

public class MaterialListTests
{
    // A Medium turbine whose default is one piece of Scrap Metal, so a fall back is easy to see.
    private static readonly TurbineKind Kind = TurbineRows.Medium with { Defaults = TurbineRows.Medium.Defaults with { Materials = "20004:1" } };

    private static bool AnyItem(int id) => true;

    private static int? NoOtherRecipe(IReadOnlyList<int> materials) => null;

    private static readonly (TurbineKind, IReadOnlyList<int>, bool)[] NothingTaken = Array.Empty<(TurbineKind, IReadOnlyList<int>, bool)>();

    [Fact]
    public void EachPairBecomesThatManyPiecesInOrder()
    {
        Assert.True(MaterialList.TryParse("20004:4, 20104:2", out var materials, out _));
        Assert.Equal(new[] { 20004, 20004, 20004, 20004, 20104, 20104 }, materials);
    }

    [Fact]
    public void AnItemListedTwiceIsAddedUp()
    {
        Assert.True(MaterialList.TryParse("20004:2; 20104:1, 20004:3", out var materials, out _));
        Assert.Equal(new[] { 20004, 20004, 20004, 20004, 20004, 20104 }, materials);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" , ")]
    [InlineData("20004")]
    [InlineData("20004:two")]
    [InlineData("20004:0")]
    [InlineData("0:2")]
    [InlineData("20004:100")]
    [InlineData("20004:60, 20004:40")]
    [InlineData("1:1, 2:1, 3:1, 4:1, 5:1, 6:1, 7:1, 8:1")]
    public void AListTheRecipeCannotUseIsRefusedWithAReason(string text)
    {
        Assert.False(MaterialList.TryParse(text, out var materials, out var problem));
        Assert.Empty(materials);
        Assert.NotEmpty(problem);
    }

    [Fact]
    public void SevenDifferentMaterialsAreAllowed()
    {
        Assert.True(MaterialList.TryParse("1:1, 2:1, 3:1, 4:1, 5:1, 6:1, 7:1", out var materials, out _));
        Assert.Equal(7, materials.Length);
    }

    [Fact]
    public void AUsableListIsChosen()
    {
        var (materials, problem) = MaterialList.Choose("20373:2", Kind, AnyItem, NoOtherRecipe, NothingTaken);

        Assert.Equal(new[] { 20373, 20373 }, materials);
        Assert.Null(problem);
    }

    [Fact]
    public void AnUnreadableListFallsBackToTheDefault()
    {
        var (materials, problem) = MaterialList.Choose("scrap", Kind, AnyItem, NoOtherRecipe, NothingTaken);

        Assert.Equal(new[] { 20004 }, materials);
        Assert.Contains("not written as item id:count", problem!.Log);
    }

    // The Mod Manager writes an emptied list as blank, so the player is told the turbine keeps its default.
    [Fact]
    public void AnEmptyListFallsBackToTheDefaultAndSaysSo()
    {
        var (materials, problem) = MaterialList.Choose(" ", Kind, AnyItem, NoOtherRecipe, NothingTaken);

        Assert.Equal(new[] { 20004 }, materials);
        Assert.Equal("The list is empty, so the turbine keeps its default materials.\n材料清单是空的，风力发电机会改用默认材料。", problem!.Player);
    }

    [Fact]
    public void AnItemTheGameDoesNotHaveFallsBackToTheDefault()
    {
        var (materials, problem) = MaterialList.Choose("20373:1, 99999:1", Kind, id => id != 99999, NoOtherRecipe, NothingTaken);

        Assert.Equal(new[] { 20004 }, materials);
        Assert.Contains("no item 99999", problem!.Log);
        Assert.StartsWith("The game has no item 99999,", problem.English);
    }

    // The game files recipes by their materials, so a list another recipe already uses would hide one of the two.
    [Fact]
    public void AnotherRecipesMaterialsFallBackToTheDefaultNamingWhatItMakes()
    {
        var (materials, problem) = MaterialList.Choose("20373:1", Kind, AnyItem, list => list.SequenceEqual(new[] { 20373 }) ? 42 : null, NothingTaken, id => id == 42 ? ("Table Saw", "台锯") : null);

        Assert.Equal(new[] { 20004 }, materials);
        Assert.Contains("recipe 42", problem!.Log);
        Assert.StartsWith("The game's Table Saw recipe takes exactly these materials.", problem.English);
        Assert.StartsWith("游戏自带的台锯配方", problem.Chinese);
    }

    [Fact]
    public void AStockRecipeWithNoNameToShowStillSaysWhy()
    {
        var (_, problem) = MaterialList.Choose("20373:1", Kind, AnyItem, _ => 42, NothingTaken);

        Assert.StartsWith("A recipe the game already has takes exactly these materials.", problem!.English);
    }

    [Fact]
    public void TheOtherTurbinesMaterialsInAnyOrderFallBackToTheDefault()
    {
        var taken = new (TurbineKind, IReadOnlyList<int>, bool)[] { (TurbineRows.Large, new[] { 20104, 20373, 20373 }, false) };
        var (materials, problem) = MaterialList.Choose("20373:2, 20104:1", Kind, AnyItem, NoOtherRecipe, taken);

        Assert.Equal(new[] { 20004 }, materials);
        Assert.Contains("the Large Wind Turbine already uses them", problem!.Log);
        Assert.StartsWith("The Large Wind Turbine already takes exactly these materials.", problem.English);
        Assert.StartsWith("大型风力发电机已经用了", problem.Chinese);
    }

    [Fact]
    public void ALaterTurbinesDefaultIsKeptForIt()
    {
        var taken = new (TurbineKind, IReadOnlyList<int>, bool)[] { (TurbineRows.Small, new[] { 20373 }, true) };
        var (_, problem) = MaterialList.Choose("20373:1", Kind, AnyItem, NoOtherRecipe, taken);

        Assert.StartsWith("These are the Small Wind Turbine's default materials", problem!.English);
    }

    // Returns: blank follows ReturnPercent, "none" gives nothing, a list is used as written, even items the recipe lacks.
    [Theory]
    [InlineData("", 50, "20004:3,20104:2,20210:1")]
    [InlineData("  ", 0, "")]
    [InlineData("none", 50, "")]
    [InlineData(" None ", 50, "")]
    [InlineData("20312:2, 20004:9", 50, "20312:2,20004:9")]
    public void TakingATurbineDownGivesBackWhatReturnsSays(string returns, int percent, string expected)
    {
        var (back, problem) = MaterialList.Returns(returns, MaterialList.Standard("20004:5, 20104:3, 20210:3, 20373:1"), percent, AnyItem);

        Assert.Equal(expected, MaterialList.Pairs(back));
        Assert.Null(problem);
    }

    [Fact]
    public void AReturnsListTheGameCannotUseGivesBackTheShareAndSaysWhy()
    {
        var made = MaterialList.Standard("20004:4");
        var (unread, unreadWhy) = MaterialList.Returns("20004", made, 50, AnyItem);
        var (missing, missingWhy) = MaterialList.Returns("99999:1", made, 50, id => id != 99999);

        Assert.Equal(new[] { 20004, 20004 }, unread);
        Assert.Contains("not written as item id:count", unreadWhy!.Log);
        Assert.Equal(new[] { 20004, 20004 }, missing);
        Assert.Equal("The game has no item 99999, so taking the turbine down gives back its Return percent share.\n游戏里没有物品 99999，拆除时改按「Return percent」返还。", missingWhy!.Player);
    }

    [Fact]
    public void ABrokenDefaultIsTheModsOwnMistakeAndThrows()
    {
        Assert.Throws<ArgumentException>(() => MaterialList.Standard("20004"));
    }

    [Theory]
    [InlineData("Medium")]
    [InlineData("Large")]
    [InlineData("Small")]
    public void EachTurbinesDefaultParses(string size)
    {
        Assert.NotEmpty(MaterialList.Standard(TurbineRows.All.Single(kind => kind.Size == size).Defaults.Materials));
    }

    // Taking a turbine down gives back about half of its materials, like the game's furniture: half of each, rounded
    // down, then the odd ones one more each until half the pieces are back.
    [Theory]
    [InlineData("20004:3, 20104:1, 20210:1", "20004:2")]
    [InlineData("20004:4, 20104:2, 20210:2", "20004:2, 20104:1, 20210:1")]
    [InlineData("20004:5, 20104:3, 20210:3, 20373:1", "20004:3, 20104:2, 20210:1")]
    public void TakingATurbineDownGivesBackAboutHalf(string made, string back)
    {
        Assert.Equal(MaterialList.Standard(back), MaterialList.GivenBack(MaterialList.Standard(made)));
    }

    // ReturnPercent: the player's share of the recipe comes back, rounded down per material, topped up to the total.
    [Theory]
    [InlineData(0, "")]
    [InlineData(25, "20004:2, 20104:1")]
    [InlineData(50, "20004:3, 20104:2, 20210:1")]
    [InlineData(75, "20004:4, 20104:3, 20210:2")]
    [InlineData(100, "20004:5, 20104:3, 20210:3, 20373:1")]
    public void TakingATurbineDownGivesBackTheSetShare(int percent, string back)
    {
        var expected = back.Length == 0 ? new int[0] : MaterialList.Standard(back);

        Assert.Equal(expected, MaterialList.GivenBack(MaterialList.Standard("20004:5, 20104:3, 20210:3, 20373:1"), percent));
    }

    [Fact]
    public void AShareOutsideNoneToAllIsHeldToIt()
    {
        var made = MaterialList.Standard("20004:5, 20104:3");

        Assert.Equal(made, MaterialList.GivenBack(made, 250));
        Assert.Empty(MaterialList.GivenBack(made, -10));
    }

    [Fact]
    public void ARecipeOfOnePieceStillGivesThatPieceBack()
    {
        Assert.Equal(new[] { 20004 }, MaterialList.GivenBack(new[] { 20004 }));
    }

    [Fact]
    public void NothingComesBackMoreThanItWentIn()
    {
        var materials = new[] { 1, 2, 3, 4, 5, 6, 7 };
        var back = MaterialList.GivenBack(materials);

        Assert.Equal(3, back.Length);
        Assert.Equal(back.Length, back.Distinct().Count());
        Assert.All(back, id => Assert.Contains(id, materials));
    }
}
