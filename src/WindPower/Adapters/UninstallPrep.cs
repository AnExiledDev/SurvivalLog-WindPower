using System;
using System.Collections.Generic;
using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;
using ExiledToolkit.Game;
using WindPower.Core;
using Il2CppItemSaves = Il2CppSystem.Collections.Generic.List<GameCore.HotUpdate.ItemSave>;
using Il2CppStock = Il2CppSystem.Collections.Generic.Dictionary<int, int>;

namespace WindPower.Adapters;

/// <summary>
/// While the Uninstall prep setting is on, turns everything of the mod's in the loaded run into what a game without
/// the mod knows (see <see cref="Uninstall"/>): the turbine packages in bags, storage and on the floor of the place the
/// player is in, the same in every other place the save holds, the shops' stock, and the pre-disaster purchase record.
/// The stock setting is held at 0 meanwhile, so the shops stop listing the turbines. Nothing it changes is undone when
/// the setting goes off again: the packages stay solar panel packages.
/// </summary>
internal static class UninstallPrep
{
    private const float LiveSeconds = 3f;
    private const float SavedSeconds = 30f;

    private static readonly HashSet<long> Touched = new();
    private static readonly List<KeyValuePair<int, int>> Records = new();

    private static volatile bool _isOn;
    private static float _nextLiveAt;
    private static float _nextSavedAt;
    private static IntPtr _sweptSave;

    internal static Action<string> Log = _ => { };

    public static bool IsOn => _isOn;

    /// <summary>Turns the sweep on or off. Safe from any thread.</summary>
    public static void Set(bool isOn)
    {
        _isOn = isOn;
        _sweptSave = IntPtr.Zero;
        _nextLiveAt = 0;
        _nextSavedAt = 0;
    }

    public static void Tick(float now)
    {
        if (!_isOn || now < _nextLiveAt) return;

        _nextLiveAt = now + LiveSeconds;

        var world = GameSingleton.Existing<BattleLogicWorld>();
        var items = world?._ItemManager;

        if (world == null || items == null) return;

        var packages = Packages(items);
        var shops = ShopStock.RemoveTurbines();
        var bought = Ledger(world._DataCollectionManager);
        var saved = 0;
        var save = GameSingleton.Existing<GameSaveManager>()?._gameSave_k__BackingField?._CurSave_k__BackingField;

        if (save != null && (save.Pointer != _sweptSave || now >= _nextSavedAt))
        {
            _sweptSave = save.Pointer;
            _nextSavedAt = now + SavedSeconds;
            saved = Saved(save);
        }

        if (packages + shops + bought + saved > 0)
            Log($"Uninstall prep: {packages} turbine packages here and {saved} saved elsewhere turned into solar panel packages, {shops} shop entries removed, {bought} purchase records moved to the solar panels. Save the game, then remove WindPower.dll.");
    }

    /// <summary>The turbine packages in the place loaded now, wherever they lie. Each bag's weight is kept right.</summary>
    private static int Packages(ItemManager items)
    {
        var cache = items._Cache_k__BackingField;

        if (cache == null) return 0;

        var count = 0;

        Touched.Clear();

        foreach (var item in cache.Values)
        {
            if (item == null || Uninstall.StockItem(item._ItemConfigId_k__BackingField, TurbineRows.All) is not int stock) continue;

            var before = items.GetItemActualWeight(item);

            item._ItemConfigId_k__BackingField = stock;

            var delta = items.GetItemActualWeight(item) - before;
            var owner = item._OwnerId_k__BackingField;

            if (delta != 0) items.UpdateOwnerBagWeight(owner, delta, stock, item._ItemCount_k__BackingField);

            Touched.Add(owner);
            count++;
        }

        foreach (var owner in Touched)
            if (owner > 0) items.SyncSingleBag(owner);

        Touched.Clear();

        return count;
    }

    /// <summary>The turbines bought before the disaster, recorded again as the solar panels of their size.</summary>
    private static int Ledger(DataCollectionManager? data)
    {
        var records = data?.GetPreDisasterFurniturePurchaseRecords();

        if (data == null || records == null) return 0;

        Records.Clear();

        foreach (var pair in records) Records.Add(new KeyValuePair<int, int>(pair.Key, pair.Value));

        var moves = Uninstall.LedgerMoves(Records, TurbineRows.All);

        foreach (var (from, to, count) in moves)
        {
            data.RemovePreDisasterFurniturePurchase(from, count);
            data.RecordPreDisasterFurniturePurchase(to, count);
        }

        Records.Clear();

        return moves.Count;
    }

    /// <summary>
    /// The save's copy of every place: the packages held by each thing saved there and its shop stock, and the shops'
    /// stock record. The place loaded now is written over from the live game when it is saved, which the sweep above
    /// has already changed.
    /// </summary>
    private static int Saved(SaveChildData save)
    {
        var count = 0;

        if (save._LeadingRole_k__BackingField is { } player) count += Agent(player);

        if (save._ChapterAgentMap_k__BackingField is { } chapters)
            foreach (var chapter in chapters.Values)
            {
                if (chapter == null) continue;

                foreach (var agent in chapter)
                    if (agent != null) count += Agent(agent);
            }

        if (save._BuildShopStockSnapshot_k__BackingField is { } snapshots)
            foreach (var stock in snapshots.Values)
                RemoveFrom(stock);

        return count;
    }

    private static int Agent(AgentSave agent)
    {
        RemoveFrom(agent._BuildShopStockDict_k__BackingField);

        return Items(agent._ItemList_k__BackingField) + Items(agent._ShredderTrayItems_k__BackingField) + Items(agent._CompressorTrayItems_k__BackingField);
    }

    private static int Items(Il2CppItemSaves? list)
    {
        if (list == null) return 0;

        var count = 0;

        foreach (var item in list)
        {
            if (item == null || Uninstall.StockItem(item._ItemConfigId_k__BackingField, TurbineRows.All) is not int stock) continue;

            item._ItemConfigId_k__BackingField = stock;
            count++;
        }

        return count;
    }

    private static void RemoveFrom(Il2CppStock? stock)
    {
        if (stock == null) return;

        foreach (var kind in TurbineRows.All)
        {
            stock.Remove(kind.Ids.Furniture);
            stock.Remove(ShopMarks.Key(kind.Ids.Furniture));
        }
    }
}
