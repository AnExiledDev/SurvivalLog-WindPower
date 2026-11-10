using System;
using System.Collections.Generic;
using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;
using ExiledToolkit.Game;
using WindPower.Core;

namespace WindPower.Adapters;

/// <summary>
/// Keeps the turbines on the shelves of the shops before the disaster in a run that is already going. A shop fills its
/// stock from its list only the first time and keeps its own counts in the save after that, so a turbine added to the
/// list, or a stock setting changed, would never reach it. When a shop is made or loaded, and whenever a setting
/// changes, these hooks add a turbine it lacks and move its count by the change in the stock setting since the shop
/// last took it, so turbines already bought stay bought (see <see cref="ShopMarks"/>, which keeps that setting in the
/// shop's saved stock). Both targets were checked in GameAssembly.dll: no relative jump or call in their first 14 bytes.
/// </summary>
internal static class ShopStock
{
    /// <summary>The shops in the world now, by pointer. A shop that is taken down has its id set to 0.</summary>
    private static readonly Dictionary<IntPtr, BuildShopComponent> Live = new();

    private const string IfMissed = "a run already going may not see the turbines in its shops";

    private static PatchKit? _hooks;

    public static void Apply(PatchKit hooks)
    {
        _hooks = hooks;

        hooks.Patch(typeof(ShopStock), typeof(BuildShopComponent), "OnInit", postfix: nameof(Fill), consequence: IfMissed);
        hooks.Patch(typeof(ShopStock), typeof(BuildShopComponent), "LoadGame", postfix: nameof(Fill), consequence: IfMissed);
    }

    /// <summary>Moves each open shop's turbines to the stock settings, after the shops' lists were changed to match.</summary>
    public static void Restock()
    {
        try
        {
            var gone = new List<IntPtr>();

            foreach (var pair in Live)
            {
                var shop = pair.Value;

                if (shop.buildShopConfigId == 0)
                {
                    gone.Add(pair.Key);

                    continue;
                }

                if (Settle(shop)) shop.SyncStockSnapshot();
            }

            foreach (var pointer in gone) Live.Remove(pointer);
        }
        catch (Exception ex)
        {
            _hooks?.Fail("restocking the turbines in the open shops", ex);
        }
    }

    /// <summary>
    /// Takes the turbines and their marks out of every open shop's stock altogether, not just down to none, so a save
    /// holds no id a game without the mod does not know. Returns how many turbine entries went.
    /// </summary>
    public static int RemoveTurbines()
    {
        var removed = 0;

        foreach (var shop in Live.Values)
        {
            if (shop.buildShopConfigId == 0 || shop.StockDict is not { } stock) continue;

            var isChanged = false;

            foreach (var kind in TurbineRows.All)
            {
                if (stock.Remove(ShopMarks.Key(kind.Ids.Furniture))) isChanged = true;

                if (!stock.Remove(kind.Ids.Furniture)) continue;

                isChanged = true;
                removed++;
            }

            if (isChanged) shop.SyncStockSnapshot();
        }

        return removed;
    }

    private static void Fill(BuildShopComponent __instance)
    {
        try
        {
            if (__instance == null || __instance.buildShopConfigId == 0) return;

            // One shop per list is in the world at a time, so an older one with the same list is gone.
            var stale = new List<IntPtr>();

            foreach (var pair in Live)
                if (pair.Key != __instance.Pointer && (pair.Value.buildShopConfigId == __instance.buildShopConfigId || pair.Value.buildShopConfigId == 0)) stale.Add(pair.Key);

            foreach (var pointer in stale) Live.Remove(pointer);

            Live[__instance.Pointer] = __instance;

            if (Settle(__instance)) __instance.SyncStockSnapshot();
        }
        catch (Exception ex)
        {
            _hooks?.Fail("stocking a shop with the turbines", ex);
        }
    }

    /// <summary>
    /// Gives the shop each turbine its list has and its stock lacks, moves each one it has by the change in the stock
    /// setting since it last took it, and marks the setting it took. Returns whether the stock changed.
    /// </summary>
    private static bool Settle(BuildShopComponent shop)
    {
        var stock = shop.StockDict;
        var shops = GameSingleton.Existing<ConfigManager>()?._Config_ShopFurniture_Dict;
        var id = shop.buildShopConfigId;

        // Uninstall prep takes the marks out, so writing them back here would leave them in the next save. Shops
        // that never sell a turbine get no marks either.
        if (UninstallPrep.IsOn || !System.Linq.Enumerable.Contains(TurbineRows.Shops, id)) return false;
        if (stock == null || shops == null || !shops.ContainsKey(id)) return false;

        var row = shops[id];
        var ids = row?.ItemIdList;
        var counts = row?.ItemCountList;
        var isChanged = false;

        foreach (var kind in TurbineRows.All)
        {
            var furniture = kind.Ids.Furniture;
            var listed = 0;

            if (ids != null)
                for (var i = 0; i < ids.Count; i++)
                    if (ids[i] == furniture) listed = counts != null && i < counts.Count ? counts[i] : 0;

            var mark = ShopMarks.Key(furniture);
            int? current = stock.ContainsKey(furniture) ? stock[furniture] : null;
            int? applied = stock.ContainsKey(mark) ? ShopMarks.Decode(stock[mark]) : null;

            if (ShopMarks.Count(current, applied, listed) is int next && next != current)
            {
                stock[furniture] = next;
                isChanged = true;
            }

            if (applied != Math.Max(0, listed))
            {
                stock[mark] = ShopMarks.Encode(listed);
                isChanged = true;
            }
        }

        return isChanged;
    }
}
