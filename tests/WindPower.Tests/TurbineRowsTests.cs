using WindPower.Core;
using Xunit;

namespace WindPower.Tests;

public class TurbineRowsTests
{
    private static TurbineKind Kind(string size) => TurbineRows.All.Single(kind => kind.Size == size);

    private static TurbinePlan Plan(TurbineKind kind) => TurbineRows.Plan(kind, MaterialList.Standard(kind.Defaults.Materials), kind.Defaults.ShopPrice);

    private static RowPlan Row(TurbinePlan plan, Table table) => plan.Rows.Single(row => row.Table == table);

    private static object Field(TurbinePlan plan, Table table, string name) => Row(plan, table).Fields.Single(field => field.Name == name).Value;

    // The game follows recipe -> package -> unpack action -> effect, and furniture -> power row. One wrong link makes a
    // recipe that crafts a solar panel, or a package that unpacks into nothing.
    [Theory]
    [InlineData("Medium")]
    [InlineData("Large")]
    [InlineData("Small")]
    public void EveryLinkPointsAtTheTurbinesOwnRows(string size)
    {
        var plan = Plan(Kind(size));

        Assert.Equal(new[] { Row(plan, Table.Item).Id }, (int[])Field(plan, Table.Recipe, "ProductID"));
        Assert.Equal(Row(plan, Table.Furniture).Id, Field(plan, Table.Item, "TargetFurnitureID"));
        Assert.Equal(Row(plan, Table.Action).Id, Field(plan, Table.Item, "UseAction"));
        Assert.Equal(Row(plan, Table.Effect).Id, Field(plan, Table.Action, "EffectConfigID"));
        Assert.Equal(Row(plan, Table.Electrical).Id, Field(plan, Table.Furniture, "ElectricalFurnitureID"));
    }

    [Theory]
    [InlineData("Medium")]
    [InlineData("Large")]
    [InlineData("Small")]
    public void EachRowSetsItsOwnIdField(string size)
    {
        var plan = Plan(Kind(size));

        foreach (var row in plan.Rows) Assert.Equal(row.Id, Field(plan, row.Table, "ID"));
    }

    [Theory]
    [InlineData("Medium")]
    [InlineData("Large")]
    [InlineData("Small")]
    public void ItCanOnlyBeInstalledInTheYardOrOnTheTerrace(string size)
    {
        Assert.Equal(new[] { 4, 8 }, ((int[])Field(Plan(Kind(size)), Table.Electrical, "AllowedRooms")).OrderBy(room => room));
    }

    [Theory]
    [InlineData("Medium")]
    [InlineData("Large")]
    [InlineData("Small")]
    public void EveryRowIsACopyOfItsSolarPanelsChainUnderANewId(string size)
    {
        var kind = Kind(size);
        var copied = Plan(kind).Rows.ToDictionary(row => row.Table, row => row.CopyFrom);

        Assert.Equal(kind.Stock.Recipe, copied[Table.Recipe]);
        Assert.Equal(kind.Stock.Item, copied[Table.Item]);
        Assert.Equal(kind.Stock.Action, copied[Table.Action]);
        Assert.Equal(kind.Stock.Effect, copied[Table.Effect]);
        Assert.Equal(kind.Stock.Furniture, copied[Table.Furniture]);
        Assert.Equal(kind.Stock.Electrical, copied[Table.Electrical]);
        Assert.All(Plan(kind).Rows, row => Assert.NotEqual(row.CopyFrom, row.Id));
    }

    // The copy decides the slot the turbine fits: each turbine takes the spot of the solar panel of its size.
    [Fact]
    public void EachTurbineCopiesTheSolarPanelOfItsSize()
    {
        Assert.Equal(40000, TurbineRows.Small.Stock.Furniture);
        Assert.Equal(40001, TurbineRows.Medium.Stock.Furniture);
        Assert.Equal(40002, TurbineRows.Large.Stock.Furniture);
    }

    // Two turbines writing to one id would leave only one of them in the game.
    [Fact]
    public void NoTwoTurbinesShareAnIdInAnyTable()
    {
        var rows = TurbineRows.All.SelectMany(kind => Plan(kind).Rows).Select(row => (row.Table, row.Id)).ToList();

        Assert.Equal(rows.Count, rows.Distinct().Count());
    }

    // GetLocalTxt shows the key itself when it is missing, and the _Local fields hold the Chinese text directly.
    [Theory]
    [InlineData("Medium")]
    [InlineData("Large")]
    [InlineData("Small")]
    public void EveryTextKeyHasEnglishAndChineseAndEachLocalFieldMatchesItsKey(string size)
    {
        var plan = Plan(Kind(size));
        var texts = plan.Texts.ToDictionary(text => text.Key);

        foreach (var row in plan.Rows)
        {
            foreach (var field in row.Fields.Where(field => field.Name.EndsWith("_Local")))
            {
                var key = (string)Field(plan, row.Table, field.Name[..^"_Local".Length]);

                Assert.True(texts.ContainsKey(key), $"{row.Table}.{field.Name}: no text for {key}");
                Assert.Equal(texts[key].Zh, field.Value);
            }
        }

        Assert.All(plan.Texts.Append(Kind(size).QuestLine), text =>
        {
            Assert.False(string.IsNullOrWhiteSpace(text.En));
            Assert.False(string.IsNullOrWhiteSpace(text.Zh));
        });
    }

    [Fact]
    public void NoTextKeyIsUsedTwice()
    {
        var keys = TurbineRows.All.SelectMany(kind => kind.Texts).Concat(TurbineRows.SharedTexts).Select(text => text.Key).ToList();

        Assert.Equal(keys.Count, keys.Distinct().Count());
    }

    // Like the solar panels: hidden until a quest reward shows it, then craftable from Production 1 on.
    [Theory]
    [InlineData("Medium")]
    [InlineData("Large")]
    [InlineData("Small")]
    public void TheRecipeIsShownByItsQuestAndCraftableFromProductionOne(string size)
    {
        var plan = Plan(Kind(size));

        Assert.Equal(0, Field(plan, Table.Recipe, "show_level"));
        Assert.Equal(1, Field(plan, Table.Recipe, "craft_level"));
    }

    [Fact]
    public void TheShopPriceIsTheOneGiven()
    {
        Assert.Equal(75, Field(TurbineRows.Plan(TurbineRows.Medium, new[] { 20004 }, 75), Table.Furniture, "FurniturePrice"));
    }

    [Fact]
    public void TheMaterialsAreTheOnesGiven()
    {
        var plan = TurbineRows.Plan(TurbineRows.Large, new[] { 20004, 20004, 20373 }, 120);

        Assert.Equal(new[] { 20004, 20004, 20373 }, (int[])Field(plan, Table.Recipe, "MaterialList"));
        Assert.Equal(new[] { 20004, 20004, 20373 }, (int[])Field(plan, Table.Recipe, "perfect_item_id"));
    }

    // The wind never stops at night, so each turbine makes less than the solar panel of its size and costs more.
    [Fact]
    public void TheDefaultsAreTheAgreedBalance()
    {
        Assert.Equal(new TurbineSettings(18f, "20004:4, 20104:2, 20210:2", 60, 2), TurbineRows.Medium.Defaults);
        Assert.Equal(new TurbineSettings(36f, "20004:5, 20104:3, 20210:3, 20373:1", 120, 2), TurbineRows.Large.Defaults);
        Assert.Equal(new TurbineSettings(9f, "20004:3, 20104:1, 20210:1", 30, 2), TurbineRows.Small.Defaults);
    }

    // The medium and small turbines come with the solar quest, the large one with the storage quest that follows it.
    [Fact]
    public void EachTurbineIsShownByItsOwnQuest()
    {
        Assert.Equal((40421, 6237, 309), (TurbineRows.Medium.Quest, TurbineRows.Medium.RewardSets[0], TurbineRows.Medium.RevealedWith));
        Assert.Equal((40422, 6238, 311), (TurbineRows.Large.Quest, TurbineRows.Large.RewardSets[0], TurbineRows.Large.RevealedWith));
        Assert.Equal((40421, 6237, 308), (TurbineRows.Small.Quest, TurbineRows.Small.RewardSets[0], TurbineRows.Small.RevealedWith));
        Assert.Equal("Event_EventDes_40421", TurbineRows.Medium.QuestLine.Key);
        Assert.Equal("Event_EventDes_40421", TurbineRows.Small.QuestLine.Key);
        Assert.Equal("Event_EventDes_40422", TurbineRows.Large.QuestLine.Key);
    }

    [Fact]
    public void AnEmptyGameWithAllStockRowsHasNoProblems()
    {
        var kind = TurbineRows.Medium;

        Assert.Empty(TurbineRows.Problems(Plan(kind), (_, id) => IsStockId(kind, id) ? Slot.Stock : Slot.Empty, NoOtherRecipe));
    }

    [Fact]
    public void RowsWeWroteEarlierCanBeWrittenAgain()
    {
        var kind = TurbineRows.Large;

        Assert.Empty(TurbineRows.Problems(Plan(kind), (_, id) => IsStockId(kind, id) ? Slot.Stock : Slot.Ours, NoOtherRecipe));
    }

    [Fact]
    public void AGameRowAtOneOfOurIdsStopsTheWholeTurbine()
    {
        var kind = TurbineRows.Medium;
        var problems = TurbineRows.Problems(Plan(kind), (table, id) => IsStockId(kind, id) || (table == Table.Recipe && id == kind.Ids.Recipe) ? Slot.Stock : Slot.Empty, NoOtherRecipe);

        Assert.Equal(new[] { $"Recipe {kind.Ids.Recipe} is already used by the game" }, problems);
    }

    [Fact]
    public void AMissingSolarPanelRowIsReported()
    {
        var kind = TurbineRows.Large;
        var problems = TurbineRows.Problems(Plan(kind), (table, id) => table == Table.Electrical && id == kind.Stock.Electrical ? Slot.Empty : IsStockId(kind, id) ? Slot.Stock : Slot.Empty, NoOtherRecipe);

        Assert.Equal(new[] { $"Electrical {kind.Stock.Electrical}, the row to copy, is missing" }, problems);
    }

    // The solar panels' materials, as recipes 308, 309 and 310 list them in 1.1.20151. The first build copied the large
    // panel's and the game filed both recipes under one key, so the turbine never showed in the crafting menu.
    private static readonly int[][] SolarMaterials =
    {
        new[] { 20002, 20002, 20002, 20004, 20004 },
        new[] { 20002, 20002, 20002, 20002, 20002, 20004, 20004, 20004, 20210, 20210 },
        new[] { 20002, 20002, 20002, 20002, 20002, 20002, 20002, 20002, 20004, 20004, 20004, 20004, 20210, 20210, 20210 },
    };

    [Theory]
    [InlineData("Medium")]
    [InlineData("Large")]
    [InlineData("Small")]
    public void TheDefaultRecipeIsNotFiledUnderASolarPanelsMaterials(string size)
    {
        var materials = (int[])Field(Plan(Kind(size)), Table.Recipe, "MaterialList");

        Assert.All(SolarMaterials, solar => Assert.NotEqual(TurbineRows.MaterialKey(solar), TurbineRows.MaterialKey(materials)));
    }

    [Fact]
    public void NoTwoDefaultRecipesAreFiledUnderOneKey()
    {
        var keys = TurbineRows.All.Select(kind => TurbineRows.MaterialKey(MaterialList.Standard(kind.Defaults.Materials))).ToList();

        Assert.Equal(keys.Count, keys.Distinct().Count());
    }

    // Taking a turbine down gives back about half of what it was made from, like the game's own furniture, so the
    // solar panel's leftovers never come out of a turbine.
    [Theory]
    [InlineData("Medium")]
    [InlineData("Large")]
    [InlineData("Small")]
    public void TakingATurbineDownGivesBackHalfOfItsOwnMaterials(string size)
    {
        var materials = new[] { 20004, 20004, 20004, 20104, 20373, 20373 };
        var plan = TurbineRows.Plan(Kind(size), materials, 50);

        Assert.Equal(new[] { 20004, 20004, 20373 }, (int[])Field(plan, Table.Furniture, "RemoveGet"));
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
        var kind = TurbineRows.Medium;
        var ours = TurbineRows.MaterialKey(MaterialList.Standard(kind.Defaults.Materials));
        var problems = TurbineRows.Problems(Plan(kind), (_, id) => IsStockId(kind, id) ? Slot.Stock : Slot.Empty, materials => TurbineRows.MaterialKey(materials) == ours ? 555 : null);

        Assert.Equal(new[] { "Recipe 555 already uses the same materials, so only one of the two could be crafted" }, problems);
    }

    private static int? NoOtherRecipe(IReadOnlyList<int> materials) => null;

    private static bool IsStockId(TurbineKind kind, int id) => new[] { kind.Stock.Item, kind.Stock.Furniture, kind.Stock.Electrical, kind.Stock.Recipe, kind.Stock.Action, kind.Stock.Effect }.Contains(id);
}
