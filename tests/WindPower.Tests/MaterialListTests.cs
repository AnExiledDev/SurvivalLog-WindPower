using WindPower.Core;
using Xunit;

namespace WindPower.Tests;

public class MaterialListTests
{
    private const string Fallback = "20004:1";

    private static bool AnyItem(int id) => true;

    private static int? NoOtherRecipe(IReadOnlyList<int> materials) => null;

    private static readonly IReadOnlyList<int>[] NothingTaken = Array.Empty<IReadOnlyList<int>>();

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
        var (materials, problem) = MaterialList.Choose("20373:2", Fallback, AnyItem, NoOtherRecipe, NothingTaken);

        Assert.Equal(new[] { 20373, 20373 }, materials);
        Assert.Null(problem);
    }

    [Fact]
    public void AnUnreadableListFallsBackToTheDefault()
    {
        var (materials, problem) = MaterialList.Choose("scrap", Fallback, AnyItem, NoOtherRecipe, NothingTaken);

        Assert.Equal(new[] { 20004 }, materials);
        Assert.Contains("not written as item id:count", problem);
    }

    [Fact]
    public void AnItemTheGameDoesNotHaveFallsBackToTheDefault()
    {
        var (materials, problem) = MaterialList.Choose("20373:1, 99999:1", Fallback, id => id != 99999, NoOtherRecipe, NothingTaken);

        Assert.Equal(new[] { 20004 }, materials);
        Assert.Contains("no item 99999", problem);
    }

    // The game files recipes by their materials, so a list another recipe already uses would hide one of the two.
    [Fact]
    public void AnotherRecipesMaterialsFallBackToTheDefault()
    {
        var (materials, problem) = MaterialList.Choose("20373:1", Fallback, AnyItem, list => list.SequenceEqual(new[] { 20373 }) ? 42 : null, NothingTaken);

        Assert.Equal(new[] { 20004 }, materials);
        Assert.Contains("recipe 42", problem);
    }

    [Fact]
    public void TheOtherTurbinesMaterialsInAnyOrderFallBackToTheDefault()
    {
        var taken = new IReadOnlyList<int>[] { new[] { 20104, 20373, 20373 } };
        var (materials, problem) = MaterialList.Choose("20373:2, 20104:1", Fallback, AnyItem, NoOtherRecipe, taken);

        Assert.Equal(new[] { 20004 }, materials);
        Assert.Contains("other wind turbine", problem);
    }

    [Fact]
    public void ABrokenDefaultIsTheModsOwnMistakeAndThrows()
    {
        Assert.Throws<ArgumentException>(() => MaterialList.Standard("20004"));
    }

    [Theory]
    [InlineData("Medium")]
    [InlineData("Large")]
    public void EachTurbinesDefaultParses(string size)
    {
        Assert.NotEmpty(MaterialList.Standard(TurbineRows.All.Single(kind => kind.Size == size).Defaults.Materials));
    }
}
