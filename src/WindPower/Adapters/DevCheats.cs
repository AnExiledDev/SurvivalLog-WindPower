using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;
using ExiledToolkit.Game;
using WindPower.Core;
using Il2CppInts = Il2CppSystem.Collections.Generic.List<int>;

namespace WindPower.Adapters;

/// <summary>
/// Test buttons for Cheat Menu's Mod tests tab. Cheat Menu finds this class by its name, so neither mod needs the other.
/// Each method is one button: its DisplayName is the label and its Description the help, as "English\nChinese". What
/// it returns is shown in Cheat Menu's log.
/// </summary>
public static class DevCheats
{
    private const string NotInGame = "load a save first";

    [DisplayName("Show turbines\n查看风力发电机")]
    [Description("Shows the weather and the wind now, and for each turbine what it makes in the yard and on the 2F terrace, whether its recipe is shown, its ids and its shop settings.\n显示当前天气和风力，以及每种风力发电机在院子和二楼露台的发电量、配方是否已显示、编号和商店设置。")]
    public static string ShowTurbines()
    {
        var (id, name) = WindPatches.Weather();

        if (id < 0) return NotInGame;

        var share = WindPatches.Wind();
        var tool = GameSingleton.Existing<BattleLogicWorld>()?._ToolTableManager;
        var recipes = GameSingleton.Existing<ConfigManager>()?._Config_ProductionList_Dict;
        var lines = new List<string> { TurbineReport.Weather(id, name, share) };

        foreach (var kind in TurbineRows.All)
        {
            bool? shown = tool != null && recipes != null && recipes.ContainsKey(kind.Ids.Recipe) ? tool.IsProductionRevealedByRecord(recipes[kind.Ids.Recipe]) : null;

            lines.Add(TurbineReport.Turbine(kind, WindSettings.Of(kind), share, shown));
        }

        return string.Join("\n", lines);
    }

    [DisplayName("Show all recipes now\n立即显示所有配方")]
    [Description("Shows every turbine's recipe at the workbench now, without the quests.\n不做任务，立即在工作台显示所有风力发电机的配方。")]
    public static string ShowAllRecipes()
    {
        var tool = GameSingleton.Existing<BattleLogicWorld>()?._ToolTableManager;
        var recipes = GameSingleton.Existing<ConfigManager>()?._Config_ProductionList_Dict;

        if (tool == null || recipes == null) return NotInGame;

        var added = new List<string>();

        foreach (var kind in TurbineRows.All)
        {
            if (!recipes.ContainsKey(kind.Ids.Recipe)) return $"{kind.Name}: recipe {kind.Ids.Recipe} is not in the game's tables yet";
            if (tool.IsProductionRevealedByRecord(recipes[kind.Ids.Recipe])) continue;

            var ids = new Il2CppInts();

            ids.Add(kind.Ids.Recipe);
            tool.AddProductionRecords(ids);
            added.Add(kind.Name);
        }

        return added.Count == 0 ? "all recipes were already shown" : $"shown now: {string.Join(", ", added)}";
    }

    [DisplayName("Give 1 of each turbine\n给每种风力发电机各 1 个")]
    [Description("Puts one Small, one Medium and one Large Wind Turbine Package in your bag.\n在背包里放入小型、中型、大型风力发电机包裹各 1 个。")]
    public static string GivePackages()
    {
        if (Bag() is not { } bag) return NotInGame;

        var lines = new List<string>();

        foreach (var kind in TurbineRows.All) lines.Add(Give(bag, kind.Ids.Item, 1, kind.Name + " Package"));

        return string.Join("\n", lines);
    }

    [DisplayName("Give materials for each turbine\n给每种风力发电机的材料")]
    [Description("Puts in your bag what crafting one of each turbine's package takes, following each turbine's Materials setting as it is now.\n按当前「Materials」设置，在背包里放入每种风力发电机包裹各制作一次所需的材料。")]
    public static string GiveMaterials()
    {
        if (Bag() is not { } bag) return NotInGame;

        var lines = new List<string>();

        foreach (var choice in ModManagerAnswers.Choices())
        {
            lines.Add($"{choice.Kind.Name}:");

            foreach (var group in choice.Materials.GroupBy(id => id)) lines.Add("  " + Give(bag, group.Key, group.Count(), NameOf(bag.Config, group.Key)));
        }

        return string.Join("\n", lines);
    }

    [DisplayName("Give 1 of each dye\n给每种染料各 1 个")]
    [Description("Puts one of each of the game's eight dyes in your bag, for crafting the coloured turbines.\n在背包里放入游戏中八种染料各 1 个，用来制作彩色风力发电机。")]
    public static string GiveDyes()
    {
        if (Bag() is not { } bag) return NotInGame;

        return string.Join("\n", Dyes.All.Select(dye => Give(bag, dye.Item, 1, dye.En + " Dye")));
    }

    [DisplayName("Show recipes and returns\n查看配方和返还")]
    [Description("Shows, for each turbine, what crafting it takes and what taking it down gives back now, and whether Returns or ReturnPercent decides that.\n显示每种风力发电机现在的制作材料、拆下后返还的物品，以及由「Returns」还是「ReturnPercent」决定。")]
    public static string ShowRecipesAndReturns()
    {
        var config = GameSingleton.Existing<ConfigManager>();

        if (config == null) return NotInGame;

        return string.Join("\n", ModManagerAnswers.Choices().Select(choice => TurbineReport.Recipe(choice, WindSettings.Of(choice.Kind), id => NameOf(config, id))));
    }

    [DisplayName("Show the wind for 12 hours\n查看未来 12 小时风力")]
    [Description("Shows the wind hour by hour for the next 12 game hours if the weather stays as it is, so you can see it rise and fall without waiting. A Variation of 0 shows it steady.\n如果天气不变，逐小时显示未来 12 个游戏小时的风力，不用等就能看到起伏。「Variation」为 0 时风力不变。")]
    public static string ShowWindForecast()
    {
        var (id, name) = WindPatches.Weather();

        if (id < 0) return NotInGame;

        var lines = new List<string> { $"weather {name} ({id}), Variation {WindSettings.VariationPercent}%" };

        lines.AddRange(TurbineReport.Forecast(WindOutput.Share(id), WindSettings.VariationPercent, WindPatches.GameSeconds(), ForecastHours));

        return string.Join("\n", lines);
    }

    private const int ForecastHours = 12;

    private const int HoursPerDay = 24;

    private sealed record PlayerBag(ConfigManager Config, ItemManager Items, long OwnerId, int Hour);

    /// <summary>The player's bag and the hour, or null outside a save.</summary>
    private static PlayerBag? Bag()
    {
        var world = GameSingleton.Existing<BattleLogicWorld>();
        var config = GameSingleton.Existing<ConfigManager>();
        var role = world?._AgentManager?.GetLeadingRole();
        var items = world?._ItemManager;

        if (config == null || role == null || items == null) return null;

        return new PlayerBag(config, items, role.InstanceId, world?._GameTimeManager?.GetHour() ?? 0);
    }

    /// <summary>Adds items the way the game gives one back, fresh from this hour, and says whether they fit.</summary>
    private static string Give(PlayerBag bag, int itemConfigId, int count, string name)
    {
        var life = bag.Config.Get_Config_Item(itemConfigId)?.Life ?? 0;
        var timeLeft = life <= 0 ? -1 : HoursPerDay * life;
        var isAdded = bag.Items.TryAddItemToBag(bag.OwnerId, itemConfigId, count, bag.Hour, timeLeft, 0, 1f, null, 0, null, null, 0, 0, false, false, false, false, 0);

        return isAdded ? $"gave {count} {name}" : $"could not give {count} {name}: the bag is full or too heavy";
    }

    private static string NameOf(ConfigManager config, int itemConfigId) => GameNames.ItemName(config, itemConfigId) ?? $"item {itemConfigId}";
}
