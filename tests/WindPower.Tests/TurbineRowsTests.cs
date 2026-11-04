using WindPower.Core;
using Xunit;

namespace WindPower.Tests;

public class TurbineRowsTests
{
    private static readonly TurbinePlan Plan = TurbineRows.Large();

    private static RowPlan Row(Table table) => Plan.Rows.Single(row => row.Table == table);

    private static object Field(Table table, string name) => Row(table).Fields.Single(field => field.Name == name).Value;

    // The game follows recipe -> package -> unpack action -> effect, and furniture -> power row. One wrong link makes a
    // recipe that crafts a solar panel, or a package that unpacks into nothing.
    [Fact]
    public void EveryLinkPointsAtTheTurbinesOwnRows()
    {
        Assert.Equal(new[] { Row(Table.Item).Id }, (int[])Field(Table.Recipe, "ProductID"));
        Assert.Equal(Row(Table.Furniture).Id, Field(Table.Item, "TargetFurnitureID"));
        Assert.Equal(Row(Table.Action).Id, Field(Table.Item, "UseAction"));
        Assert.Equal(Row(Table.Effect).Id, Field(Table.Action, "EffectConfigID"));
        Assert.Equal(Row(Table.Electrical).Id, Field(Table.Furniture, "ElectricalFurnitureID"));
    }

    [Fact]
    public void EachRowSetsItsOwnIdField()
    {
        foreach (var row in Plan.Rows) Assert.Equal(row.Id, Field(row.Table, "ID"));
    }

    [Fact]
    public void ItCanOnlyBeInstalledInTheYardOrOnTheTerrace()
    {
        Assert.Equal(new[] { 4, 8 }, ((int[])Field(Table.Electrical, "AllowedRooms")).OrderBy(room => room));
    }

    [Fact]
    public void EveryRowIsACopyOfTheLargeSolarPanelsChainUnderANewId()
    {
        var copied = Plan.Rows.ToDictionary(row => row.Table, row => row.CopyFrom);

        Assert.Equal(TurbineRows.Stock.Recipe, copied[Table.Recipe]);
        Assert.Equal(TurbineRows.Stock.Item, copied[Table.Item]);
        Assert.Equal(TurbineRows.Stock.Furniture, copied[Table.Furniture]);
        Assert.Equal(TurbineRows.Stock.Electrical, copied[Table.Electrical]);
        Assert.All(Plan.Rows, row => Assert.NotEqual(row.CopyFrom, row.Id));
    }

    // GetLocalTxt shows the key itself when it is missing, and the _Local fields hold the Chinese text directly.
    [Fact]
    public void EveryTextKeyHasEnglishAndChineseAndEachLocalFieldMatchesItsKey()
    {
        var texts = Plan.Texts.ToDictionary(text => text.Key);

        foreach (var row in Plan.Rows)
        {
            foreach (var field in row.Fields.Where(field => field.Name.EndsWith("_Local")))
            {
                var key = (string)Field(row.Table, field.Name[..^"_Local".Length]);

                Assert.True(texts.ContainsKey(key), $"{row.Table}.{field.Name}: no text for {key}");
                Assert.Equal(texts[key].Zh, field.Value);
            }
        }

        Assert.All(Plan.Texts, text =>
        {
            Assert.False(string.IsNullOrWhiteSpace(text.En));
            Assert.False(string.IsNullOrWhiteSpace(text.Zh));
        });
    }

    [Fact]
    public void AnEmptyGameWithAllStockRowsHasNoProblems()
    {
        Assert.Empty(TurbineRows.Problems(Plan, (_, id) => IsStockId(id) ? Slot.Stock : Slot.Empty, NoOtherRecipe));
    }

    [Fact]
    public void RowsWeWroteEarlierCanBeWrittenAgain()
    {
        Assert.Empty(TurbineRows.Problems(Plan, (_, id) => IsStockId(id) ? Slot.Stock : Slot.Ours, NoOtherRecipe));
    }

    [Fact]
    public void AGameRowAtOneOfOurIdsStopsTheWholeTurbine()
    {
        var problems = TurbineRows.Problems(Plan, (table, id) => IsStockId(id) || (table == Table.Recipe && id == TurbineRows.Ids.Recipe) ? Slot.Stock : Slot.Empty, NoOtherRecipe);

        Assert.Equal(new[] { $"Recipe {TurbineRows.Ids.Recipe} is already used by the game" }, problems);
    }

    [Fact]
    public void AMissingSolarPanelRowIsReported()
    {
        var problems = TurbineRows.Problems(Plan, (table, id) => table == Table.Electrical && id == TurbineRows.Stock.Electrical ? Slot.Empty : IsStockId(id) ? Slot.Stock : Slot.Empty, NoOtherRecipe);

        Assert.Equal(new[] { $"Electrical {TurbineRows.Stock.Electrical}, the row to copy, is missing" }, problems);
    }

    // The Large Solar Panel's materials, as recipe 310 lists them in 1.1.20151. The first build copied them and the
    // game filed both recipes under one key, so the turbine never showed in the crafting menu.
    private static readonly int[] SolarMaterials = { 20002, 20002, 20002, 20002, 20002, 20002, 20002, 20002, 20004, 20004, 20004, 20004, 20210, 20210, 20210 };

    [Fact]
    public void TheRecipeIsNotFiledUnderTheSolarPanelsMaterials()
    {
        var materials = (int[])Field(Table.Recipe, "MaterialList");

        Assert.NotEqual(TurbineRows.MaterialKey(SolarMaterials), TurbineRows.MaterialKey(materials));
        Assert.Equal(materials, (int[])Field(Table.Recipe, "perfect_item_id"));
    }

    [Fact]
    public void TheRecipeIsListedWithoutHavingToBeDiscovered()
    {
        Assert.True((int)Field(Table.Recipe, "show_level") > 0);
    }

    [Fact]
    public void MaterialKeyIgnoresOrderButNotCount()
    {
        Assert.Equal(TurbineRows.MaterialKey(new[] { 3, 1, 2, 1 }), TurbineRows.MaterialKey(new[] { 1, 1, 2, 3 }));
        Assert.NotEqual(TurbineRows.MaterialKey(new[] { 1, 2 }), TurbineRows.MaterialKey(new[] { 1, 2, 2 }));
    }

    [Fact]
    public void AnotherRecipeWithTheSameMaterialsStopsTheWholeTurbine()
    {
        var ours = TurbineRows.MaterialKey(TurbineRows.Materials);
        var problems = TurbineRows.Problems(Plan, (_, id) => IsStockId(id) ? Slot.Stock : Slot.Empty, materials => TurbineRows.MaterialKey(materials) == ours ? 555 : null);

        Assert.Equal(new[] { "Recipe 555 already uses the same materials, so only one of the two could be crafted" }, problems);
    }

    private static int? NoOtherRecipe(IReadOnlyList<int> materials) => null;

    private static bool IsStockId(int id) => id is TurbineRows.Stock.Item or TurbineRows.Stock.Furniture or TurbineRows.Stock.Electrical or TurbineRows.Stock.Recipe or TurbineRows.Stock.Action or TurbineRows.Stock.Effect;
}
