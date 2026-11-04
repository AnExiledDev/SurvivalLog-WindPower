using System;
using System.Collections.Generic;
using System.Linq;

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

/// <summary>One field to set on a row, by the name the table gives it: an int, a string, a bool or an int[] list.</summary>
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
/// The table rows that make the Large Wind Turbine: the recipe, the package it crafts, the unpack action and its effect,
/// the furniture, and its power row. Each is a copy of the Large Solar Panel's row with its links and text changed, so
/// everything the solar panel does that is not named here (model, icon, install time, what removing it gives back) the
/// turbine does too.
/// </summary>
public static class TurbineRows
{
    /// <summary>Config_MapSlot rooms: 4 is the 2F terrace, 8 the yard outside. The same pair as the Transceiver.</summary>
    public static readonly IReadOnlyList<int> OutdoorRooms = new[] { 4, 8 };

    public static class Stock
    {
        public const int Item = 14011;
        public const int Furniture = 40002;
        public const int Electrical = 6;
        public const int Recipe = 310;
        public const int Action = 100014011;
        public const int Effect = 100014011;
    }

    /// <summary>Far above the game's own ids (1.1.20151: item 70152, furniture 90291, recipe 1703, power row 19), so an update is unlikely to reach them.</summary>
    public static class Ids
    {
        public const int Item = 910001;
        public const int Furniture = 910001;
        public const int Electrical = 910;
        public const int Recipe = 9101;
        public const int Action = 100910001;
        public const int Effect = 100910001;
    }

    private const string PackageKey = "WindPower_Large_Package";
    private const string NameKey = "WindPower_Large_Name";
    private const string DesKey = "WindPower_Large_Des";
    private const string UnpackKey = "WindPower_Large_Unpack";
    private const string UnpackingKey = "WindPower_Large_Unpacking";

    public static TurbinePlan Large()
    {
        var texts = new[]
        {
            new Text(PackageKey, "Large Wind Turbine Package", "大型风力发电机包裹"),
            new Text(NameKey, "Large Wind Turbine", "大型风力发电机"),
            new Text(DesKey,
                "Large wind turbine. It turns day and night and makes more power the stronger the wind blows. It can only be installed outdoors: in the yard outside or on the 2F terrace.",
                "大型风力发电机，昼夜都能转动，风越大发电越多。只能装在户外：屋外空地或二楼露台。"),
            new Text(UnpackKey, "Unpack Large Wind Turbine Package", "拆封 大型风力发电机包裹"),
            new Text(UnpackingKey, "Unpacking Large Wind Turbine Package...", "拆封 大型风力发电机包裹中…"),
        };

        string Zh(string key) => texts.First(text => text.Key == key).Zh;

        var rows = new[]
        {
            new RowPlan(Table.Recipe, Ids.Recipe, Stock.Recipe, new[]
            {
                new RowField("ID", Ids.Recipe),
                new RowField("ShopName", PackageKey),
                new RowField("ShopName_Local", Zh(PackageKey)),
                new RowField("ProductID", new[] { Ids.Item }),
            }),
            new RowPlan(Table.Item, Ids.Item, Stock.Item, new[]
            {
                new RowField("ID", Ids.Item),
                new RowField("ItemName", PackageKey),
                new RowField("ItemName_Local", Zh(PackageKey)),
                new RowField("TargetFurnitureID", Ids.Furniture),
                new RowField("UseAction", Ids.Action),
            }),
            new RowPlan(Table.Action, Ids.Action, Stock.Action, new[]
            {
                new RowField("ID", Ids.Action),
                new RowField("Name", UnpackKey),
                new RowField("Name_Local", Zh(UnpackKey)),
                new RowField("NameDoing", UnpackingKey),
                new RowField("NameDoing_Local", Zh(UnpackingKey)),
                new RowField("EffectConfigID", Ids.Effect),
            }),
            new RowPlan(Table.Effect, Ids.Effect, Stock.Effect, new[]
            {
                new RowField("ID", Ids.Effect),
            }),
            new RowPlan(Table.Furniture, Ids.Furniture, Stock.Furniture, new[]
            {
                new RowField("ID", Ids.Furniture),
                new RowField("Name", NameKey),
                new RowField("Name_Local", Zh(NameKey)),
                new RowField("Des", DesKey),
                new RowField("Des_Local", Zh(DesKey)),
                new RowField("ElectricalFurnitureID", Ids.Electrical),
            }),
            new RowPlan(Table.Electrical, Ids.Electrical, Stock.Electrical, new[]
            {
                new RowField("ID", Ids.Electrical),
                new RowField("AllowedRooms", OutdoorRooms.ToArray()),
            }),
        };

        return new TurbinePlan(rows, texts);
    }

    /// <summary>
    /// Why the plan cannot be written, or nothing if it can. Writing is all or nothing: a turbine missing its recipe or
    /// its power row is worse than no turbine, and a stock row at one of our ids must never be replaced.
    /// </summary>
    public static List<string> Problems(TurbinePlan plan, Func<Table, int, Slot> slotAt)
    {
        var problems = new List<string>();

        foreach (var row in plan.Rows)
        {
            if (slotAt(row.Table, row.CopyFrom) == Slot.Empty) problems.Add($"{row.Table} {row.CopyFrom}, the row to copy, is missing");

            if (slotAt(row.Table, row.Id) == Slot.Stock) problems.Add($"{row.Table} {row.Id} is already used by the game");
        }

        return problems;
    }
}
