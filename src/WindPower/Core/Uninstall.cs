using System.Collections.Generic;

namespace WindPower.Core;

/// <summary>
/// What Uninstall prep turns the turbines' own ids into, so a save made with it on loads cleanly without the mod. A
/// game without the mod drops an item it does not know when it loads, so a turbine package becomes the solar panel
/// package of its size (the package rows are copies of the panels', so its size and weight stay the same). A shop
/// stops selling the turbines, and the pre-disaster purchase record counts each one bought as its solar panel.
/// Placed and packed turbines need nothing here: every save already writes them as solar panels.
/// </summary>
public static class Uninstall
{
    /// <summary>
    /// The solar panel package a turbine package becomes, a coloured one the solar panel's package of the same colour,
    /// or null for any other item.
    /// </summary>
    public static int? StockItem(int itemId, IReadOnlyList<TurbineKind> kinds)
    {
        for (var i = 0; i < kinds.Count; i++)
            if (kinds[i].Ids.Item == itemId) return kinds[i].Stock.Item;

        return Dyes.OfPackage(itemId, kinds) is { } dyed ? Dyes.StockIds(dyed.Kind, dyed.Dye).Item : null;
    }

    /// <summary>The solar panel a turbine's furniture id becomes, or null for any other furniture.</summary>
    public static int? StockFurniture(int furnitureId, IReadOnlyList<TurbineKind> kinds)
    {
        for (var i = 0; i < kinds.Count; i++)
            if (kinds[i].Ids.Furniture == furnitureId) return kinds[i].Stock.Furniture;

        return null;
    }

    /// <summary>
    /// The moves that turn the turbines in a purchase record into their solar panels: take <c>Count</c> off
    /// <c>From</c> and add it to <c>To</c>. Records of anything else stay as they are.
    /// </summary>
    public static List<(int From, int To, int Count)> LedgerMoves(IEnumerable<KeyValuePair<int, int>> records, IReadOnlyList<TurbineKind> kinds)
    {
        var moves = new List<(int From, int To, int Count)>();

        foreach (var record in records)
            if (record.Value > 0 && StockFurniture(record.Key, kinds) is int stock) moves.Add((record.Key, stock, record.Value));

        return moves;
    }
}
