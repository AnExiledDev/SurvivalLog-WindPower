using System;
using System.Collections.Generic;
using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;
using HarmonyLib;
using ExiledToolkit.Game;
using WindPower.Core;

namespace WindPower.Adapters;

/// <summary>
/// Keeps the turbines on the shelves of the shops before the disaster in a run that is already going. A shop fills its
/// stock from its list only the first time and keeps its own counts in the save after that, so a turbine added to the
/// list, or a stock setting changed, would never reach it. When a shop is made or loaded, these hooks add a turbine it
/// lacks and bring one it has too many of down to the setting; a stock setting changed while a shop is open moves its
/// count by the difference, so turbines already bought stay bought. Both targets were checked in GameAssembly.dll: no
/// relative jump or call in their first 14 bytes.
/// </summary>
internal static class ShopStock
{
    /// <summary>The shops in the world now, by pointer. A shop that is taken down has its id set to 0.</summary>
    private static readonly Dictionary<IntPtr, BuildShopComponent> Live = new();

    /// <summary>The stock setting last applied to each turbine, by furniture id.</summary>
    private static readonly Dictionary<int, int> Applied = new();

    private static readonly HashSet<string> Failed = new();
    private static Action<string> _log = _ => { };

    public static void Apply(Harmony harmony, Action<string> log)
    {
        _log = log;

        Hook(harmony, "OnInit");
        Hook(harmony, "LoadGame");
    }

    /// <summary>Moves each open shop's turbines to the stock settings in <paramref name="wanted"/>, after the shops' lists were changed to match.</summary>
    public static void Restock(IReadOnlyList<(int Furniture, int Stock)> wanted)
    {
        try
        {
            var moves = new List<(int Furniture, int Before, int After)>();

            foreach (var (furniture, stock) in wanted)
            {
                if (Applied.TryGetValue(furniture, out var before) && before != stock) moves.Add((furniture, before, stock));

                Applied[furniture] = stock;
            }

            var gone = new List<IntPtr>();

            foreach (var pair in Live)
            {
                var shop = pair.Value;

                if (shop.buildShopConfigId == 0)
                {
                    gone.Add(pair.Key);

                    continue;
                }

                var isChanged = Move(shop, moves);

                if (Settle(shop) || isChanged) shop.SyncStockSnapshot();
            }

            foreach (var pointer in gone) Live.Remove(pointer);
        }
        catch (Exception ex)
        {
            Fail("restock", ex);
        }
    }

    /// <summary>
    /// Takes the turbines out of every open shop's stock altogether, not just down to none, so a save holds no id a game
    /// without the mod does not know. Returns how many entries went.
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
            Fail("shop stock", ex);
        }
    }

    private static bool Move(BuildShopComponent shop, List<(int Furniture, int Before, int After)> moves)
    {
        var stock = shop.StockDict;
        var isChanged = false;

        if (stock == null) return false;

        foreach (var (furniture, before, after) in moves)
        {
            if (!stock.ContainsKey(furniture)) continue;

            var current = stock[furniture];
            var next = Unlocks.NewStock(current, before, after);

            if (next == current) continue;

            stock[furniture] = next;
            isChanged = true;
        }

        return isChanged;
    }

    /// <summary>
    /// Gives the shop each turbine its list has and its stock lacks, and takes any turbine down to what the list has
    /// (none, for a turbine the list leaves out). Returns whether the stock changed.
    /// </summary>
    private static bool Settle(BuildShopComponent shop)
    {
        var stock = shop.StockDict;
        var shops = GameSingleton.Existing<ConfigManager>()?._Config_ShopFurniture_Dict;
        var id = shop.buildShopConfigId;

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

            if (!stock.ContainsKey(furniture))
            {
                if (listed <= 0) continue;

                stock[furniture] = listed;
                isChanged = true;
            }
            else if (stock[furniture] > listed)
            {
                stock[furniture] = Math.Max(0, listed);
                isChanged = true;
            }
        }

        return isChanged;
    }

    private static void Hook(Harmony harmony, string method)
    {
        try
        {
            var target = AccessTools.Method(typeof(BuildShopComponent), method);

            if (target == null)
            {
                _log($"cannot find BuildShopComponent.{method}, so a run already going may not see the turbines in its shops");

                return;
            }

            harmony.Patch(target, postfix: new HarmonyMethod(typeof(ShopStock), nameof(Fill)));
        }
        catch (Exception ex)
        {
            _log($"cannot hook BuildShopComponent.{method}: {ex.Message}");
        }
    }

    private static void Fail(string where, Exception ex)
    {
        if (Failed.Add(where)) _log($"the turbines' {where} step failed (reported once): {ex}");
    }
}
