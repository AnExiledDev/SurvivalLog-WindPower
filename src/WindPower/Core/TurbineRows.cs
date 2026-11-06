using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace WindPower.Core;

public enum Table
{
    Item,
    Furniture,
    Electrical,
    Recipe,
    Action,
    Effect,
}

/// <summary>One field to set on a row, by the name the table gives it: an int, a float, a string, a bool or an int[] list.</summary>
public sealed record RowField(string Name, object Value);

/// <summary>A row to add: a copy of the stock row <paramref name="CopyFrom"/> under <paramref name="Id"/>, with these fields changed.</summary>
public sealed record RowPlan(Table Table, int Id, int CopyFrom, IReadOnlyList<RowField> Fields);

public sealed record Text(string Key, string En, string Zh);

public sealed record TurbinePlan(IReadOnlyList<RowPlan> Rows, IReadOnlyList<Text> Texts);

/// <summary>What the game's tables hold at one id, as far as writing our rows is concerned.</summary>
public enum Slot
{
    Empty,
    Ours,
    Stock,
}

/// <summary>
/// The three turbines and the table rows that make each one: the recipe, the package it crafts, the unpack action and
/// its effect, the furniture, and its power row. Each is a copy of the solar panel of its size's row with its links,
/// text and parts given back changed, so everything the panel does that is not named here (install time, slot size)
/// the turbine does too.
/// </summary>
public static class TurbineRows
{
    /// <summary>Config_MapSlot rooms: 4 is the 2F terrace, 8 the yard outside. The same pair as the Transceiver.</summary>
    public static readonly IReadOnlyList<int> OutdoorRooms = new[] { 4, 8 };

    /// <summary>Config_MapSlot room id of the 2F terrace.</summary>
    public const int TerraceRoom = 4;

    /// <summary>The pre-disaster furniture shops (Config_ShopFurniture), which sell both solar panels.</summary>
    public static readonly IReadOnlyList<int> Shops = new[] { 1, 14, 15 };

    public const string WebIconFolder = "Res/Mods/WindPower";

    /// <summary>The wind part of the power hover's note: {0} the weather, {1} the output as a share of the rating.</summary>
    public const string WindNoteKey = "WindPower_WindNote";

    /// <summary>Added to the power hover's note for a turbine on the 2F terrace.</summary>
    public const string TerraceNoteKey = "WindPower_TerraceNote";

    /// <summary>The wind row's label in the HUD's weather tooltip, next to the game's own Light Intensity.</summary>
    public const string WindLabelKey = "WindPower_WindLabel";

    public static readonly IReadOnlyList<Text> SharedTexts = new[]
    {
        new Text(WindNoteKey, "{0} wind {1}", "{0}风力 {1}"),
        new Text(TerraceNoteKey, "2F terrace +20%", "二楼露台 +20%"),
        new Text(WindLabelKey, "Wind Intensity", "风力强度"),
    };

    /// <summary>
    /// A Savonius rotor: two half drums on an upright shaft. It is revealed with the Medium Solar Panel, from the
    /// solar quest on, and makes half the large turbine's power.
    /// </summary>
    public static readonly TurbineKind Medium = new()
    {
        Name = "Medium Wind Turbine",
        Size = "Medium",
        // Far above the game's own ids (1.1.20151: item 70152, furniture 90291, recipe 1703, power row 19), so an
        // update is unlikely to reach them.
        Ids = new RowIds(Item: 910002, Furniture: 910002, Electrical: 911, Recipe: 9102, Action: 100910002, Effect: 100910002),
        Stock = new RowIds(Item: 14010, Furniture: 40001, Electrical: 5, Recipe: 309, Action: 100014010, Effect: 100014010),
        StockDyedItem = 14756,
        StockDyedRecipe = 1656,
        ResName = "Mod_WindTurbine_M",
        StockPrefab = "P_RoofEquipment_009_S1_M01_Middle",
        ModelFile = "WindTurbineMedium.glb",
        IconFile = "UI_Item_Icon_WindTurbine_M.png",
        Movers = new Dictionary<string, Vector3> { ["Rotor"] = Vector3.UnitY },
        TurnsIntoGusts = false,
        SpinScale = 0.6f,
        RevealedWith = 309,
        RewardSets = new[] { 6237, 66011, 48000 },
        Quest = 40421,
        QuestLine = new Text("Event_EventDes_40421",
            "The Scrap Metal could catch the wind as well: bent into two half drums and set on a shaft, it would turn day and night. A Medium Wind Turbine might be worth building too.",
            "铁皮也能用来接风：弯成两个半圆筒装在转轴上，风一吹就能日夜转个不停。做一台中型风力发电机，应该也值得。"),
        SaveMarker = "WindPower.Turbine.Medium:",
        Defaults = new TurbineSettings(RatedPower: 18f, Materials: "20004:4, 20104:2, 20210:2", ShopPrice: 60, ShopStock: 2),
        Texts = new[]
        {
            new Text("WindPower_Medium_Package", "Medium Wind Turbine Package", "中型风力发电机包裹"),
            new Text("WindPower_Medium_Name", "Medium Wind Turbine", "中型风力发电机"),
            new Text("WindPower_Medium_Des",
                "Medium wind turbine. Its two half drums catch wind from any side, so it turns day and night and makes more power the stronger the wind blows. It can only be installed outdoors: in the yard outside or on the 2F terrace.",
                "中型风力发电机，两片半圆筒四面来风都能接住，昼夜都能转动，风越大发电越多。只能装在户外：屋外空地或二楼露台。"),
            new Text("WindPower_Medium_Unpack", "Unpack Medium Wind Turbine Package", "拆封 中型风力发电机包裹"),
            new Text("WindPower_Medium_Unpacking", "Unpacking Medium Wind Turbine Package...", "拆封 中型风力发电机包裹中…"),
        },
    };

    /// <summary>A three-blade turbine on a mast, whose top swings round into gusts. It is revealed with the batteries, from the storage quest on.</summary>
    public static readonly TurbineKind Large = new()
    {
        Name = "Large Wind Turbine",
        Size = "Large",
        Ids = new RowIds(Item: 910001, Furniture: 910001, Electrical: 910, Recipe: 9101, Action: 100910001, Effect: 100910001),
        Stock = new RowIds(Item: 14011, Furniture: 40002, Electrical: 6, Recipe: 310, Action: 100014011, Effect: 100014011),
        StockDyedItem = 14764,
        StockDyedRecipe = 1664,
        ResName = "Mod_WindTurbine_L",
        StockPrefab = "P_RoofEquipment_009_S1_M02_Large",
        ModelFile = "WindTurbineLarge.glb",
        IconFile = "UI_Item_Icon_WindTurbine_L.png",
        // three.js spins a rotor about X.
        Movers = new Dictionary<string, Vector3> { ["Head"] = Vector3.UnitY, ["Rotor"] = Vector3.UnitX },
        TurnsIntoGusts = true,
        SpinScale = 1f,
        RevealedWith = 311,
        RewardSets = new[] { 6238, 66011, 48000 },
        Quest = 40422,
        QuestLine = new Text("Event_EventDes_40422",
            "And the wind keeps blowing after dark, when the panels go quiet. With somewhere to store the power, a Large Wind Turbine would keep it coming all night.",
            "天黑后太阳能板歇了，风却还在吹。有了存电的地方，再造一台大型风力发电机，整夜都能有电进来。"),
        // The marker 0.1 to 0.3 wrote, kept so their saves still load as turbines.
        SaveMarker = "WindPower.Turbine:",
        Defaults = new TurbineSettings(RatedPower: 36f, Materials: "20004:5, 20104:3, 20210:3, 20373:1", ShopPrice: 120, ShopStock: 2),
        Texts = new[]
        {
            new Text("WindPower_Large_Package", "Large Wind Turbine Package", "大型风力发电机包裹"),
            new Text("WindPower_Large_Name", "Large Wind Turbine", "大型风力发电机"),
            new Text("WindPower_Large_Des",
                "Large wind turbine. It turns day and night and makes more power the stronger the wind blows. It can only be installed outdoors: in the yard outside or on the 2F terrace.",
                "大型风力发电机，昼夜都能转动，风越大发电越多。只能装在户外：屋外空地或二楼露台。"),
            new Text("WindPower_Large_Unpack", "Unpack Large Wind Turbine Package", "拆封 大型风力发电机包裹"),
            new Text("WindPower_Large_Unpacking", "Unpacking Large Wind Turbine Package...", "拆封 大型风力发电机包裹中…"),
        },
    };

    /// <summary>
    /// A small three-blade rotor on a guyed pole, with a tail that turns it into the wind. It fits the yard's Small
    /// spots (the terrace has none) and is revealed with the Small Solar Panel, from the solar quest on.
    /// </summary>
    public static readonly TurbineKind Small = new()
    {
        Name = "Small Wind Turbine",
        Size = "Small",
        Ids = new RowIds(Item: 910003, Furniture: 910003, Electrical: 912, Recipe: 9103, Action: 100910003, Effect: 100910003),
        Stock = new RowIds(Item: 14009, Furniture: 40000, Electrical: 1, Recipe: 308, Action: 100014009, Effect: 100014009),
        StockDyedItem = 14748,
        StockDyedRecipe = 1648,
        ResName = "Mod_WindTurbine_S",
        StockPrefab = "P_RoofEquipment_009_S1_M03",
        ModelFile = "WindTurbineSmall.glb",
        IconFile = "UI_Item_Icon_WindTurbine_S.png",
        Movers = new Dictionary<string, Vector3> { ["Head"] = Vector3.UnitY, ["Rotor"] = Vector3.UnitX },
        TurnsIntoGusts = true,
        // A small rotor turns faster than a big one in the same wind.
        SpinScale = 1.4f,
        RevealedWith = 308,
        RewardSets = new[] { 6237, 66011, 48000 },
        Quest = 40421,
        QuestLine = new Text("Event_EventDes_40421",
            "A smaller one would do for the yard's small spots: three blades and a tail on a pole, a Small Wind Turbine.",
            "屋外空地的小位置也能用上：杆子上装三片叶片和一个尾翼，就是一台小型风力发电机。"),
        SaveMarker = "WindPower.Turbine.Small:",
        Defaults = new TurbineSettings(RatedPower: 9f, Materials: "20004:3, 20104:1, 20210:1", ShopPrice: 30, ShopStock: 2),
        Texts = new[]
        {
            new Text("WindPower_Small_Package", "Small Wind Turbine Package", "小型风力发电机包裹"),
            new Text("WindPower_Small_Name", "Small Wind Turbine", "小型风力发电机"),
            new Text("WindPower_Small_Des",
                "Small wind turbine. Its tail keeps it facing the wind, so it turns day and night and makes more power the stronger the wind blows. It can only be installed in a small spot in the yard outside.",
                "小型风力发电机，尾翼让它始终迎着风，昼夜都能转动，风越大发电越多。只能装在屋外空地的小型位置上。"),
            new Text("WindPower_Small_Unpack", "Unpack Small Wind Turbine Package", "拆封 小型风力发电机包裹"),
            new Text("WindPower_Small_Unpacking", "Unpacking Small Wind Turbine Package...", "拆封 小型风力发电机包裹中…"),
        },
    };

    // The small one last, so its line in the solar quest comes after the medium one's and older settings keep their place.
    public static readonly IReadOnlyList<TurbineKind> All = new[] { Medium, Large, Small };

    /// <summary>The key the game files a recipe under: its materials, sorted.</summary>
    public static string MaterialKey(IEnumerable<int> materials) => string.Join(",", materials.OrderBy(id => id));

    /// <param name="materials">The recipe's materials, each id once per piece.</param>
    /// <param name="price">What the pre-disaster shop asks for it, in gold.</param>
    /// <param name="givenBack">What taking it down gives back, each id once per piece; by default half the materials.</param>
    public static TurbinePlan Plan(TurbineKind kind, IReadOnlyList<int> materials, int price, IReadOnlyList<int>? givenBack = null)
    {
        var ids = kind.Ids;
        var package = kind.Key("Package");
        var name = kind.Key("Name");
        var des = kind.Key("Des");
        var unpack = kind.Key("Unpack");
        var unpacking = kind.Key("Unpacking");

        string Zh(string key) => kind.Texts.First(text => text.Key == key).Zh;

        var rows = new[]
        {
            new RowPlan(Table.Recipe, ids.Recipe, kind.Stock.Recipe, new[]
            {
                new RowField("ID", ids.Recipe),
                new RowField("ShopName", package),
                new RowField("ShopName_Local", Zh(package)),
                new RowField("ProductID", new[] { ids.Item }),
                new RowField("MaterialList", materials.ToArray()),
                new RowField("perfect_item_id", materials.ToArray()),
                // Hidden until a quest reward or the mod's check shows it, then craftable from Production 1 on, like
                // the solar panels.
                new RowField("show_level", 0),
                new RowField("craft_level", 1),
            }),
            new RowPlan(Table.Item, ids.Item, kind.Stock.Item, new[]
            {
                new RowField("ID", ids.Item),
                new RowField("ItemName", package),
                new RowField("ItemName_Local", Zh(package)),
                new RowField("TargetFurnitureID", ids.Furniture),
                new RowField("UseAction", ids.Action),
                new RowField("WebIcon", kind.WebIcon),
            }),
            new RowPlan(Table.Action, ids.Action, kind.Stock.Action, new[]
            {
                new RowField("ID", ids.Action),
                new RowField("Name", unpack),
                new RowField("Name_Local", Zh(unpack)),
                new RowField("NameDoing", unpacking),
                new RowField("NameDoing_Local", Zh(unpacking)),
                new RowField("EffectConfigID", ids.Effect),
            }),
            new RowPlan(Table.Effect, ids.Effect, kind.Stock.Effect, new[]
            {
                new RowField("ID", ids.Effect),
            }),
            new RowPlan(Table.Furniture, ids.Furniture, kind.Stock.Furniture, new[]
            {
                new RowField("ID", ids.Furniture),
                new RowField("Name", name),
                new RowField("Name_Local", Zh(name)),
                new RowField("Des", des),
                new RowField("Des_Local", Zh(des)),
                new RowField("ElectricalFurnitureID", ids.Electrical),
                new RowField("ResName", kind.ResName),
                new RowField("ICON", kind.WebIcon),
                new RowField("FurniturePrice", price),
                // What taking it down gives back. The copied row would give the solar panel's parts.
                new RowField("RemoveGet", (givenBack ?? MaterialList.GivenBack(materials)).ToArray()),
            }),
            new RowPlan(Table.Electrical, ids.Electrical, kind.Stock.Electrical, new[]
            {
                new RowField("ID", ids.Electrical),
                new RowField("AllowedRooms", OutdoorRooms.ToArray()),
                // Kept a solar-type row so the game lists, saves and shows it like a panel, with no sunlight power of
                // its own: the wind power is added by the mod, and the solar talent has nothing to multiply.
                new RowField("BasePower", 0f),
            }),
        };

        return new TurbinePlan(rows, kind.Texts);
    }

    /// <summary>
    /// Why the plan cannot be written, or nothing if it can. Writing is all or nothing: a turbine missing its recipe or
    /// its power row is worse than no turbine, and a stock row at one of our ids must never be replaced.
    /// </summary>
    /// <param name="recipeUsing">The id of another recipe made of these materials in any order, or null.</param>
    public static List<string> Problems(TurbinePlan plan, Func<Table, int, Slot> slotAt, Func<IReadOnlyList<int>, int?> recipeUsing)
    {
        var problems = new List<string>();

        foreach (var row in plan.Rows)
        {
            if (slotAt(row.Table, row.CopyFrom) == Slot.Empty) problems.Add($"{row.Table} {row.CopyFrom}, the row to copy, is missing");

            if (slotAt(row.Table, row.Id) == Slot.Stock) problems.Add($"{row.Table} {row.Id} is already used by the game");

            if (row.Fields.FirstOrDefault(field => field.Name == "MaterialList")?.Value is int[] materials && recipeUsing(materials) is int other)
                problems.Add($"Recipe {other} already uses the same materials, so only one of the two could be crafted");
        }

        return problems;
    }
}
