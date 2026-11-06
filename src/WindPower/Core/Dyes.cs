using System.Collections.Generic;
using System.Linq;

namespace WindPower.Core;

/// <summary>One of the game's eight dyes: its colour slot (1 to 8), the dye item, and its name as the packages carry it.</summary>
public sealed record Dye(int Slot, int Item, string En, string Zh)
{
    /// <summary>The colour the dye shader draws, from <see cref="Paint.GameDyes"/>.</summary>
    public DyeColour Colour => Paint.GameDyes[Slot - 1];
}

/// <summary>
/// The coloured turbine packages, made the way the game makes its coloured solar panels (game 1.1.20719): for each dye
/// a recipe of the plain recipe's materials plus that dye, crafting a package of its own that unpacks into the same
/// turbine, dyed. The game pairs a coloured recipe with its plain one by those materials and shows the dyes on the
/// plain recipe's card (<c>ConfigManager::EnsureDyedFurnitureMaps</c>). Each row is a copy of the solar panel's
/// coloured row of the same size and slot.
/// </summary>
public static class Dyes
{
    public static readonly IReadOnlyList<Dye> All = new Dye[]
    {
        new(1, 20363, "Sage Green", "鼠尾草绿"),
        new(2, 20364, "Terracotta", "陶土橙"),
        new(3, 20365, "Cream Yellow", "奶油黄"),
        new(4, 20366, "Dusk Blue", "暮蓝"),
        new(5, 20368, "Cherry Blossom", "樱花粉"),
        new(6, 20367, "Oat White", "燕麦白"),
        new(7, 20369, "Mist Purple", "雾紫"),
        new(8, 20370, "Ochre Red", "赭红"),
    };

    public static bool IsDye(int item) => All.Any(dye => dye.Item == item);

    /// <summary>
    /// The ids of a turbine's coloured rows for one dye: the package item 100 per slot above the plain one, the recipe
    /// the plain one's times ten plus the slot, and the action and effect 100000000 above the item, as the game numbers
    /// its own. The furniture and power row are the plain turbine's.
    /// </summary>
    public static RowIds Ids(RowIds plain, Dye dye)
    {
        var item = plain.Item + 100 * dye.Slot;

        return new RowIds(item, plain.Furniture, plain.Electrical, plain.Recipe * 10 + dye.Slot, 100000000 + item, 100000000 + item);
    }

    /// <summary>The solar panel's coloured rows for this dye, which the turbine's are copied from.</summary>
    public static RowIds StockIds(TurbineKind kind, Dye dye)
    {
        var item = kind.StockDyedItem + dye.Slot - 1;

        return new RowIds(item, kind.Stock.Furniture, kind.Stock.Electrical, kind.StockDyedRecipe + dye.Slot - 1, 100000000 + item, 100000000 + item);
    }

    /// <summary>The dye a coloured package of any turbine is in, with that turbine, or null for any other item.</summary>
    public static (TurbineKind Kind, Dye Dye)? OfPackage(int item, IReadOnlyList<TurbineKind> kinds)
    {
        foreach (var kind in kinds)
        foreach (var dye in All)
            if (Ids(kind.Ids, dye).Item == item) return (kind, dye);

        return null;
    }

    public static string IconFile(TurbineKind kind, Dye dye) => kind.IconFile.Replace(".png", $"_D{dye.Slot}.png");

    public static string WebIcon(TurbineKind kind, Dye dye) => "../../" + TurbineRows.WebIconFolder + "/" + IconFile(kind, dye);

    public static string Key(TurbineKind kind, Dye dye, string part) => kind.Key($"{part}_D{dye.Slot}");

    /// <summary>The names the coloured rows point at: "Sage Green Large Wind Turbine Package" and its unpack lines.</summary>
    public static IReadOnlyList<Text> Texts(TurbineKind kind, Dye dye)
    {
        var package = kind.Texts.First(text => text.Key == kind.Key("Package"));
        var en = $"{dye.En} {package.En}";
        var zh = dye.Zh + package.Zh;

        return new[]
        {
            new Text(Key(kind, dye, "Package"), en, zh),
            new Text(Key(kind, dye, "Unpack"), "Unpack " + en, "拆封 " + zh),
            new Text(Key(kind, dye, "Unpacking"), $"Unpacking {en}...", $"拆封 {zh}中…"),
        };
    }

    /// <summary>The four rows of one coloured package: its recipe, the package, and its unpack action and effect.</summary>
    public static IReadOnlyList<RowPlan> Rows(TurbineKind kind, Dye dye, IReadOnlyList<int> materials)
    {
        var ids = Ids(kind.Ids, dye);
        var stock = StockIds(kind, dye);
        var texts = Texts(kind, dye);
        var recipe = materials.Append(dye.Item).ToArray();

        return new[]
        {
            new RowPlan(Table.Recipe, ids.Recipe, stock.Recipe, new[]
            {
                new RowField("ID", ids.Recipe),
                new RowField("ShopName", texts[0].Key),
                new RowField("ShopName_Local", texts[0].Zh),
                new RowField("ProductID", new[] { ids.Item }),
                new RowField("MaterialList", recipe),
                new RowField("perfect_item_id", recipe),
            }),
            new RowPlan(Table.Item, ids.Item, stock.Item, new[]
            {
                new RowField("ID", ids.Item),
                new RowField("ItemName", texts[0].Key),
                new RowField("ItemName_Local", texts[0].Zh),
                new RowField("TargetFurnitureID", ids.Furniture),
                new RowField("UseAction", ids.Action),
                new RowField("WebIcon", WebIcon(kind, dye)),
            }),
            new RowPlan(Table.Action, ids.Action, stock.Action, new[]
            {
                new RowField("ID", ids.Action),
                new RowField("Name", texts[1].Key),
                new RowField("Name_Local", texts[1].Zh),
                new RowField("NameDoing", texts[2].Key),
                new RowField("NameDoing_Local", texts[2].Zh),
                new RowField("WebIcon", WebIcon(kind, dye)),
                new RowField("EffectConfigID", ids.Effect),
            }),
            new RowPlan(Table.Effect, ids.Effect, stock.Effect, new[]
            {
                new RowField("ID", ids.Effect),
            }),
        };
    }
}
