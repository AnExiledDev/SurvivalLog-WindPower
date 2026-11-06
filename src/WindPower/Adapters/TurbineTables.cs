using System;
using System.Collections.Generic;
using System.Linq;
using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;
using Il2CppInterop.Runtime.InteropTypes;
using ExiledToolkit.Game;
using WindPower.Core;
using Il2CppDictionary = Il2CppSystem.Collections.Generic;
using Il2CppInts = Il2CppSystem.Collections.Generic.List<int>;

namespace WindPower.Adapters;

/// <summary>
/// Writes the turbines' rows and text into the game's tables once they are loaded, then rebuilds the game's lookups
/// that were made from them, and brings the turbines into a run: the quest rewards that show the solar panels and
/// batteries show the turbines too, the two quests say so, and the shops before the disaster sell them. It writes again
/// when the game loads a table afresh or a setting changes, over its own rows where they already are. Every few
/// seconds it also shows a turbine's recipe to a player who was already shown the recipe it comes with.
/// </summary>
internal sealed class TurbineTables
{
    private const float CheckSeconds = 1f;
    private const float RevealSeconds = 5f;

    // The six row tables, the reward sets, the quests, the shop lists, and the two languages.
    private const int Watched = 11;

    private readonly Action<string> _log;
    private readonly IntPtr[] _seen = new IntPtr[Watched];
    private readonly IntPtr[] _applied = new IntPtr[Watched];
    private readonly List<Reveal> _reveals = new();
    private HashSet<IntPtr> _ours = new();
    private IReadOnlyList<TurbineSettings>? _appliedSettings;
    private float _nextCheckAt;
    private float _nextRevealAt;

    public TurbineTables(Action<string> log)
    {
        _log = log;
    }

    public void Tick(float now)
    {
        if (now >= _nextRevealAt)
        {
            _nextRevealAt = now + RevealSeconds;
            ShowRecipes();
        }

        if (now < _nextCheckAt) return;

        _nextCheckAt = now + CheckSeconds;

        var config = GameSingleton.Existing<ConfigManager>();
        var english = config?.customCache?.EnglishLanguageMap;
        var chinese = config?.customCache?.ChineseLanguageMap;

        if (config == null || english == null || chinese == null) return;

        KeepDyeable(config);

        var tables = new Tables(config);

        if (!tables.HasRows) return;

        tables.Pointers(_seen, english, chinese);

        var settings = WindSettings.Current;

        if (ReferenceEquals(settings, _appliedSettings) && _seen.AsSpan().SequenceEqual(_applied)) return;

        // Marked as applied only once the write is through, so one that fails partway is tried again at the next check.
        Write(config, tables, english, chinese, settings);
        _seen.CopyTo(_applied, 0);
        _appliedSettings = settings;
    }

    private void Write(ConfigManager config, Tables tables, Il2CppDictionary.Dictionary<string, string> english, Il2CppDictionary.Dictionary<string, string> chinese, IReadOnlyList<TurbineSettings> settings)
    {
        var views = new Dictionary<Table, ITableView>
        {
            [Table.Item] = new TableView<Config_Item>(tables.Items!, () => new Config_Item()),
            [Table.Furniture] = new TableView<Config_Furniture>(tables.Furniture!, () => new Config_Furniture()),
            [Table.Electrical] = new TableView<Config_FurnitureElectrical>(tables.Electrical!, () => new Config_FurnitureElectrical()),
            [Table.Recipe] = new TableView<Config_ProductionList>(tables.Recipes!, () => new Config_ProductionList()),
            [Table.Action] = new TableView<Config_Action>(tables.Actions!, () => new Config_Action()),
            [Table.Effect] = new TableView<Config_Effect>(tables.Effects!, () => new Config_Effect()),
        };

        // Our rows from the last write, where they still are: a table loaded afresh has none of them.
        var before = _ours;

        _ours = new HashSet<IntPtr>();

        foreach (var kind in TurbineRows.All)
        foreach (var ids in Dyes.All.Select(dye => Dyes.Ids(kind.Ids, dye)).Prepend(kind.Ids))
        foreach (var (table, id) in RowsOf(ids))
            views[table].Keep(id, before, _ours);

        SetTexts(TurbineRows.SharedTexts, english, chinese);

        var written = new List<(TurbineKind Kind, TurbineSettings Settings, int[] Materials)>();
        var items = tables.Items!;
        var recipes = tables.Recipes!;
        var choices = TurbineChoices.Of(settings, items.ContainsKey, list => RecipeUsing(recipes, list, recipe => _ours.Contains(recipe.Pointer)));

        for (var i = 0; i < TurbineRows.All.Count; i++)
        {
            var kind = TurbineRows.All[i];
            var wanted = settings[i];
            var (_, materials, problem, back, returnsProblem, _, _) = choices[i];

            if (problem != null) _log($"{kind.Name}: {problem.Log}, so it takes its default materials");

            if (returnsProblem != null) _log($"{kind.Name}: {returnsProblem.Log}, so taking it down gives back {wanted.ReturnPercent}% of its materials");

            var plan = TurbineRows.Plan(kind, materials, wanted.ShopPrice, back);
            var problems = TurbineRows.Problems(plan, (table, id) => views[table].SlotAt(id, _ours), list => RecipeUsing(recipes, list, recipe => _ours.Contains(recipe.Pointer)));

            if (problems.Count > 0)
            {
                foreach (var reason in problems) _log($"cannot add the {kind.Name}: {reason}");

                continue;
            }

            SetTexts(plan.Texts, english, chinese);

            var refused = new List<string>();

            foreach (var row in plan.Rows) refused.AddRange(views[row.Table].Write(row, _ours));

            foreach (var reason in refused) _log($"{kind.Name} field not set: {reason}");

            WriteDyed(kind, materials, views, recipes, english, chinese);
            written.Add((kind, wanted, materials));
        }

        if (written.Count == 0) return;

        // The recipe lookup is built once from the recipe table, and the "where to get it" lookups rebuild themselves
        // when this one is empty.
        config.InitProductionListMap();
        config._furnitureToPackageItems = null;
        // The coloured recipe lookups rebuild themselves when this is empty.
        config._dyedRecipeToBase = null;

        var kinds = written.Select(done => done.Kind).ToList();

        Step("quest rewards", () => AddToRewards(tables.RewardSets, kinds));
        Step("quest text", () => AddToQuests(tables.Events, kinds, english, chinese));
        Step("shop lists", () => AddToShops(tables.Shops, written.Select(done => (done.Kind.Ids.Furniture, done.Settings.ShopStock)).ToList()));
        ShopStock.Restock();
        KeepReveals(recipes, kinds);

        foreach (var (kind, wanted, materials) in written)
        {
            var ids = kind.Ids;

            _log($"{kind.Name} added: item {ids.Item}, furniture {ids.Furniture}, recipe {ids.Recipe}, power row {ids.Electrical}; rated {wanted.RatedPower}, materials {MaterialText(materials)}, price {wanted.ShopPrice}, stock {wanted.ShopStock}");
        }
    }

    /// <summary>
    /// Writes a turbine's coloured packages, one for each dye whose solar panel rows are there to copy. A dye that
    /// cannot be written is left out and the others still are: the plain turbine works without them.
    /// </summary>
    private void WriteDyed(TurbineKind kind, int[] materials, Dictionary<Table, ITableView> views, Il2CppDictionary.Dictionary<int, Config_ProductionList> recipes, Il2CppDictionary.Dictionary<string, string> english, Il2CppDictionary.Dictionary<string, string> chinese)
    {
        var skipped = new List<string>();

        foreach (var dye in Dyes.All)
        {
            var plan = new TurbinePlan(Dyes.Rows(kind, dye, materials), Dyes.Texts(kind, dye));
            var problems = TurbineRows.Problems(plan, (table, id) => views[table].SlotAt(id, _ours), list => RecipeUsing(recipes, list, recipe => _ours.Contains(recipe.Pointer)));

            if (problems.Count > 0)
            {
                skipped.Add($"{dye.En} ({string.Join("; ", problems)})");

                continue;
            }

            SetTexts(plan.Texts, english, chinese);

            foreach (var row in plan.Rows)
            foreach (var reason in views[row.Table].Write(row, _ours))
                _log($"{dye.En} {kind.Name} field not set: {reason}");
        }

        if (skipped.Count > 0) _log($"the {kind.Name} cannot be crafted in these colours: {string.Join(", ", skipped)}");
    }

    /// <summary>
    /// Puts the turbines' prefab names on the game's list of furniture that can be dyed, which it loads once a run
    /// starts, and makes the game rebuild its lookup from it. Three lookups a second while they are there.
    /// </summary>
    private void KeepDyeable(ConfigManager config)
    {
        var names = config._dyeableResNames;

        if (names == null) return;

        var stale = false;

        foreach (var kind in TurbineRows.All)
            if (!names.Contains(kind.ResName)) stale |= names.Add(kind.ResName);

        // A lookup built before our rows were written lacks them too.
        var ids = config._dyeableFurnitureIds;
        var furniture = config._Config_Furniture_Dict;

        if (ids != null && furniture != null && config._dyeableSourceDict?.Pointer == furniture.Pointer)
            foreach (var kind in TurbineRows.All)
                if (furniture.ContainsKey(kind.Ids.Furniture) && !ids.Contains(kind.Ids.Furniture)) stale = true;

        if (!stale) return;

        // The game rebuilds its list of dyeable ids when this no longer points at the furniture table.
        config._dyeableSourceDict = null;
        _log("the turbines can be dyed");
    }

    /// <summary>Adds each turbine's recipe to the quest rewards that show its solar panel or battery.</summary>
    private void AddToRewards(Il2CppDictionary.Dictionary<int, Config_RewardSet>? sets, IReadOnlyList<TurbineKind> kinds)
    {
        if (sets == null || sets.Count == 0) return;

        foreach (var setId in kinds.SelectMany(kind => kind.RewardSets).Distinct())
        {
            if (!sets.ContainsKey(setId))
            {
                _log($"reward set {setId} is missing, so it does not show a turbine's recipe");

                continue;
            }

            var set = sets[setId];
            var next = Unlocks.WithRecipes(ToArray(set.ShowProductionList), kinds.Where(kind => kind.RewardSets.Contains(setId)).Select(kind => kind.Ids.Recipe));

            if (next != null) set.ShowProductionList = RowFields.ToList(next);
        }
    }

    /// <summary>Adds each turbine's line at the end of its quest's text, in both languages and in the quest row itself.</summary>
    private void AddToQuests(Il2CppDictionary.Dictionary<int, Config_Event>? events, IReadOnlyList<TurbineKind> kinds, Il2CppDictionary.Dictionary<string, string> english, Il2CppDictionary.Dictionary<string, string> chinese)
    {
        if (events == null || events.Count == 0) return;

        foreach (var kind in kinds)
        {
            if (!events.ContainsKey(kind.Quest))
            {
                _log($"quest {kind.Quest} is missing, so its text does not mention the {kind.Name}");

                continue;
            }

            var quest = events[kind.Quest];
            var key = string.IsNullOrEmpty(quest.EventDes) ? kind.QuestLine.Key : quest.EventDes;

            if (english.ContainsKey(key) && Unlocks.WithLine(english[key], kind.QuestLine.En, "\n") is { } en) english[key] = en;

            if (chinese.ContainsKey(key) && Unlocks.WithLine(chinese[key], kind.QuestLine.Zh, "\n") is { } zh) chinese[key] = zh;

            if (Unlocks.WithLine(quest.EventDes_Local, kind.QuestLine.Zh, "\\n") is { } local) quest.EventDes_Local = local;
        }
    }

    /// <summary>Lists each turbine in the shops before the disaster with its stock, or takes it off when its stock is 0.</summary>
    private void AddToShops(Il2CppDictionary.Dictionary<int, Config_ShopFurniture>? shops, IReadOnlyList<(int Furniture, int Stock)> wanted)
    {
        if (shops == null || shops.Count == 0) return;

        var ours = TurbineRows.All.Select(kind => kind.Ids.Furniture).ToArray();

        foreach (var id in TurbineRows.Shops)
        {
            if (!shops.ContainsKey(id))
            {
                _log($"shop {id} is missing, so it does not sell the turbines");

                continue;
            }

            var shop = shops[id];

            if (Unlocks.Shop(ToArray(shop.ItemIdList), ToArray(shop.ItemCountList), ours, wanted) is not { } lists) continue;

            shop.ItemIdList = RowFields.ToList(lists.Ids);
            shop.ItemCountList = RowFields.ToList(lists.Counts);
        }
    }

    private void KeepReveals(Il2CppDictionary.Dictionary<int, Config_ProductionList> recipes, IReadOnlyList<TurbineKind> kinds)
    {
        _reveals.Clear();

        foreach (var kind in kinds)
            if (recipes.ContainsKey(kind.Ids.Recipe) && recipes.ContainsKey(kind.RevealedWith)) _reveals.Add(new Reveal(kind, recipes[kind.Ids.Recipe], recipes[kind.RevealedWith]));
    }

    /// <summary>
    /// Shows a turbine's recipe to a player who has the recipe it comes with: a run that was past the quest's start
    /// before the mod was added or updated, or a run whose materials setting changed, since the game remembers a
    /// recipe by its materials.
    /// </summary>
    private void ShowRecipes()
    {
        if (_reveals.Count == 0) return;

        try
        {
            var tool = GameSingleton.Existing<BattleLogicWorld>()?._ToolTableManager;

            if (tool == null) return;

            foreach (var reveal in _reveals)
            {
                if (tool.IsProductionRevealedByRecord(reveal.Ours) || !tool.IsProductionRevealedByRecord(reveal.ComesWith)) continue;

                var ids = new Il2CppInts();

                ids.Add(reveal.Kind.Ids.Recipe);
                tool.AddProductionRecords(ids);
                _log($"{reveal.Kind.Name} recipe shown, since recipe {reveal.Kind.RevealedWith} already is");
            }
        }
        catch (Exception ex)
        {
            _reveals.Clear();
            _log($"cannot show the turbines' recipes in a run already going, so they wait for the quest: {ex}");
        }
    }

    private void Step(string what, Action step)
    {
        try
        {
            step();
        }
        catch (Exception ex)
        {
            _log($"the turbines' {what} could not be changed: {ex}");
        }
    }

    /// <summary>The id of a recipe other than ours made of these materials in any order, or null.</summary>
    internal static int? RecipeUsing(Il2CppDictionary.Dictionary<int, Config_ProductionList> recipes, IReadOnlyList<int> materials, Func<Config_ProductionList, bool> isOurs)
    {
        var key = TurbineRows.MaterialKey(materials);

        foreach (var pair in recipes)
        {
            var recipe = pair.Value;

            if (recipe == null || isOurs(recipe) || recipe.MaterialList == null || recipe.MaterialList.Count != materials.Count) continue;

            if (TurbineRows.MaterialKey(ToArray(recipe.MaterialList)) == key) return pair.Key;
        }

        return null;
    }

    private static void SetTexts(IEnumerable<Text> texts, Il2CppDictionary.Dictionary<string, string> english, Il2CppDictionary.Dictionary<string, string> chinese)
    {
        foreach (var text in texts)
        {
            english[text.Key] = text.En;
            chinese[text.Key] = text.Zh;
        }
    }

    private static string MaterialText(IEnumerable<int> materials) => string.Join(", ", materials.GroupBy(id => id).Select(group => $"{group.Key}:{group.Count()}"));

    private static (Table Table, int Id)[] RowsOf(RowIds ids) => new[]
    {
        (Table.Item, ids.Item),
        (Table.Furniture, ids.Furniture),
        (Table.Electrical, ids.Electrical),
        (Table.Recipe, ids.Recipe),
        (Table.Action, ids.Action),
        (Table.Effect, ids.Effect),
    };

    private static int[] ToArray(Il2CppInts? list)
    {
        if (list == null) return Array.Empty<int>();

        var values = new int[list.Count];

        for (var i = 0; i < values.Length; i++) values[i] = list[i];

        return values;
    }

    private sealed record Reveal(TurbineKind Kind, Config_ProductionList Ours, Config_ProductionList ComesWith);

    /// <summary>The tables the mod writes, read from the game once a check.</summary>
    private sealed class Tables
    {
        public Tables(ConfigManager config)
        {
            Items = config._Config_Item_Dict;
            Furniture = config._Config_Furniture_Dict;
            Electrical = config._Config_FurnitureElectrical_Dict;
            Recipes = config._Config_ProductionList_Dict;
            Actions = config._Config_Action_Dict;
            Effects = config._Config_Effect_Dict;
            RewardSets = config._Config_RewardSet_Dict;
            Events = config._Config_Event_Dict;
            Shops = config._Config_ShopFurniture_Dict;
        }

        public Il2CppDictionary.Dictionary<int, Config_Item>? Items { get; }
        public Il2CppDictionary.Dictionary<int, Config_Furniture>? Furniture { get; }
        public Il2CppDictionary.Dictionary<int, Config_FurnitureElectrical>? Electrical { get; }
        public Il2CppDictionary.Dictionary<int, Config_ProductionList>? Recipes { get; }
        public Il2CppDictionary.Dictionary<int, Config_Action>? Actions { get; }
        public Il2CppDictionary.Dictionary<int, Config_Effect>? Effects { get; }
        public Il2CppDictionary.Dictionary<int, Config_RewardSet>? RewardSets { get; }
        public Il2CppDictionary.Dictionary<int, Config_Event>? Events { get; }
        public Il2CppDictionary.Dictionary<int, Config_ShopFurniture>? Shops { get; }

        /// <summary>Whether all six row tables are loaded. The others are optional: without one, its step is skipped.</summary>
        public bool HasRows => IsLoaded(Items) && IsLoaded(Furniture) && IsLoaded(Electrical) && IsLoaded(Recipes) && IsLoaded(Actions) && IsLoaded(Effects);

        public void Pointers(IntPtr[] into, Il2CppObjectBase english, Il2CppObjectBase chinese)
        {
            into[0] = Pointer(Items);
            into[1] = Pointer(Furniture);
            into[2] = Pointer(Electrical);
            into[3] = Pointer(Recipes);
            into[4] = Pointer(Actions);
            into[5] = Pointer(Effects);
            into[6] = Pointer(RewardSets);
            into[7] = Pointer(Events);
            into[8] = Pointer(Shops);
            into[9] = english.Pointer;
            into[10] = chinese.Pointer;
        }

        private static bool IsLoaded<T>(Il2CppDictionary.Dictionary<int, T>? table) => table != null && table.Count > 0;

        private static IntPtr Pointer(Il2CppObjectBase? table) => table?.Pointer ?? IntPtr.Zero;
    }

    private interface ITableView
    {
        Slot SlotAt(int id, HashSet<IntPtr> ours);

        /// <summary>Carries the row at <paramref name="id"/> into <paramref name="next"/> when it was one of ours.</summary>
        void Keep(int id, HashSet<IntPtr> before, HashSet<IntPtr> next);

        /// <summary>Writes the row, over our own row at its id when there is one, and returns the fields that could not be set.</summary>
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

        public Slot SlotAt(int id, HashSet<IntPtr> ours)
        {
            if (!_table.ContainsKey(id)) return Slot.Empty;

            return ours.Contains(_table[id].Pointer) ? Slot.Ours : Slot.Stock;
        }

        public void Keep(int id, HashSet<IntPtr> before, HashSet<IntPtr> next)
        {
            if (_table.ContainsKey(id) && _table[id] is { } row && before.Contains(row.Pointer)) next.Add(row.Pointer);
        }

        public List<string> Write(RowPlan plan, HashSet<IntPtr> ours)
        {
            // Writing over our own row keeps every reference the game already holds to it up to date.
            var row = SlotAt(plan.Id, ours) == Slot.Ours ? _table[plan.Id] : _make();

            RowFields.CopyAll(_table[plan.CopyFrom], row);

            var refused = plan.Fields.Select(field => RowFields.Set(row, field.Name, field.Value)).OfType<string>().ToList();

            _table[plan.Id] = row;
            ours.Add(row.Pointer);

            return refused;
        }
    }
}
