using System;
using System.Collections.Generic;
using System.ComponentModel;
using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;
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

        return added.Count == 0 ? "both recipes were already shown" : $"shown now: {string.Join(", ", added)}";
    }
}
