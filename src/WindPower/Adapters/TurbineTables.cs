using System;
using System.Collections.Generic;
using System.Linq;
using GameCore.HotUpdate;
using Il2CppInterop.Runtime.InteropTypes;
using WindPower.Core;
using Il2CppDictionary = Il2CppSystem.Collections.Generic;

namespace WindPower.Adapters;

/// <summary>
/// Writes the turbine's rows and text into the game's tables once they are loaded, then rebuilds the game's lookups
/// that were made from them. It writes again only when the game loads a table afresh.
/// </summary>
internal sealed class TurbineTables
{
    private const float CheckSeconds = 1f;

    private readonly Action<string> _log;
    private readonly HashSet<IntPtr> _ours = new();
    private IntPtr[] _applied = Array.Empty<IntPtr>();
    private float _nextCheckAt;

    public TurbineTables(Action<string> log)
    {
        _log = log;
    }

    public void Tick(float now)
    {
        if (now < _nextCheckAt) return;

        _nextCheckAt = now + CheckSeconds;

        var config = GameSingleton.Existing<ConfigManager>();
        var english = config?.customCache?.EnglishLanguageMap;
        var chinese = config?.customCache?.ChineseLanguageMap;

        if (config == null || english == null || chinese == null) return;

        var views = new Dictionary<Table, ITableView?>
        {
            [Table.Item] = View(config._Config_Item_Dict, () => new Config_Item()),
            [Table.Furniture] = View(config._Config_Furniture_Dict, () => new Config_Furniture()),
            [Table.Electrical] = View(config._Config_FurnitureElectrical_Dict, () => new Config_FurnitureElectrical()),
            [Table.Recipe] = View(config._Config_ProductionList_Dict, () => new Config_ProductionList()),
            [Table.Action] = View(config._Config_Action_Dict, () => new Config_Action()),
            [Table.Effect] = View(config._Config_Effect_Dict, () => new Config_Effect()),
        };

        if (views.Values.Any(view => view == null)) return;

        var pointers = views.Values.Select(view => view!.Pointer).Append(english.Pointer).Append(chinese.Pointer).ToArray();

        if (pointers.SequenceEqual(_applied)) return;

        _applied = pointers;
        Write(config, views.ToDictionary(pair => pair.Key, pair => pair.Value!), english, chinese);
    }

    private void Write(ConfigManager config, Dictionary<Table, ITableView> views, Il2CppDictionary.Dictionary<string, string> english, Il2CppDictionary.Dictionary<string, string> chinese)
    {
        var plan = TurbineRows.Large();
        var problems = TurbineRows.Problems(plan, (table, id) => views[table].SlotAt(id, _ours));

        if (problems.Count > 0)
        {
            foreach (var problem in problems) _log("cannot add the Large Wind Turbine: " + problem);

            return;
        }

        foreach (var text in plan.Texts)
        {
            english[text.Key] = text.En;
            chinese[text.Key] = text.Zh;
        }

        var refused = new List<string>();

        foreach (var row in plan.Rows) refused.AddRange(views[row.Table].Write(row, _ours));

        // The recipe lookup is built once from the recipe table, and the "where to get it" lookups rebuild themselves
        // when this one is empty.
        config.InitProductionListMap();
        config._furnitureToPackageItems = null;

        foreach (var reason in refused) _log("Large Wind Turbine field not set: " + reason);

        _log($"Large Wind Turbine added: item {TurbineRows.Ids.Item}, furniture {TurbineRows.Ids.Furniture}, recipe {TurbineRows.Ids.Recipe}, power row {TurbineRows.Ids.Electrical}");
    }

    private static ITableView? View<T>(Il2CppDictionary.Dictionary<int, T>? table, Func<T> make) where T : Il2CppObjectBase =>
        table == null || table.Count == 0 ? null : new TableView<T>(table, make);

    private interface ITableView
    {
        IntPtr Pointer { get; }

        Slot SlotAt(int id, HashSet<IntPtr> ours);

        /// <summary>Writes the row and returns the fields that could not be set.</summary>
        List<string> Write(RowPlan plan, HashSet<IntPtr> ours);
    }

    private sealed class TableView<T> : ITableView where T : Il2CppObjectBase
    {
        private readonly Il2CppDictionary.Dictionary<int, T> _table;
        private readonly Func<T> _make;

        public TableView(Il2CppDictionary.Dictionary<int, T> table, Func<T> make)
        {
            _table = table;
            _make = make;
        }

        public IntPtr Pointer => _table.Pointer;

        public Slot SlotAt(int id, HashSet<IntPtr> ours)
        {
            if (!_table.ContainsKey(id)) return Slot.Empty;

            return ours.Contains(_table[id].Pointer) ? Slot.Ours : Slot.Stock;
        }

        public List<string> Write(RowPlan plan, HashSet<IntPtr> ours)
        {
            var row = _make();

            RowFields.CopyAll(_table[plan.CopyFrom], row);

            var refused = plan.Fields.Select(field => RowFields.Set(row, field.Name, field.Value)).OfType<string>().ToList();

            _table[plan.Id] = row;
            ours.Add(row.Pointer);

            return refused;
        }
    }
}
