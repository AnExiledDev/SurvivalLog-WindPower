using System;
using System.Collections.Generic;
using System.Linq;
using GameCore.HotUpdate;
using ExiledToolkit.Game;
using WindPower.Core;

namespace WindPower.Adapters;

/// <summary>
/// The Mod Manager's questions about the turbines' Materials and Returns lists. The Mod Manager finds this class by its
/// name, so neither mod needs the other. It asks again after every change, so each answer is worked out afresh from
/// the settings as they stand and the game's item and recipe tables, the same way the turbines' rows are written.
/// Before the game's tables are loaded, every item counts as known and no stock recipe as taken.
/// </summary>
public static class ModManagerAnswers
{
    /// <summary>Each turbine's settings as the config holds them now, in the order of <see cref="TurbineRows.All"/>.</summary>
    internal static Func<IReadOnlyList<TurbineSettings>>? Settings { get; set; }

    public static string Problem(string section, string key) => TurbineChoices.Problem(Choices(), section, key);

    public static string Blank(string section, string key) => TurbineChoices.Blank(Choices(), section, key);

    public static string BlankNote(string section, string key) => TurbineChoices.BlankNote(Choices(), section, key);

    // One reading of the settings asks three questions of each list, and each answer looks through every recipe, so
    // the answers are kept until a setting or a table changes.
    private static TurbineSettings[] _answeredFor = Array.Empty<TurbineSettings>();
    private static (IntPtr Items, IntPtr Recipes) _answeredTables;
    private static IReadOnlyList<TurbineChoice> _answers = Array.Empty<TurbineChoice>();

    private static IReadOnlyList<TurbineChoice> Choices()
    {
        var settings = (Settings?.Invoke() ?? TurbineRows.All.Select(kind => kind.Defaults).ToList()).ToArray();
        var config = GameSingleton.Existing<ConfigManager>();
        var items = config?._Config_Item_Dict;
        var recipes = config?._Config_ProductionList_Dict;
        var tables = (items?.Pointer ?? IntPtr.Zero, recipes?.Pointer ?? IntPtr.Zero);

        if (_answers.Count > 0 && tables == _answeredTables && settings.SequenceEqual(_answeredFor)) return _answers;

        var ourRecipes = TurbineRows.All.Select(kind => kind.Ids.Recipe).ToHashSet();
        Func<int, bool> isItem = items != null && items.Count > 0 ? items.ContainsKey : _ => true;
        Func<IReadOnlyList<int>, int?> recipeUsing = recipes != null && recipes.Count > 0 ? list => TurbineTables.RecipeUsing(recipes, list, recipe => ourRecipes.Contains(recipe.ID)) : _ => null;

        _answers = TurbineChoices.Of(settings, isItem, recipeUsing, id => RecipeName(config, id));
        _answeredFor = settings;
        _answeredTables = tables;

        return _answers;
    }

    /// <summary>The name of the first thing a recipe makes, in both languages, or null when it cannot be read.</summary>
    private static (string English, string Chinese)? RecipeName(ConfigManager? config, int recipeId)
    {
        try
        {
            var recipes = config?._Config_ProductionList_Dict;
            var items = config?._Config_Item_Dict;

            if (config == null || recipes == null || items == null || !recipes.ContainsKey(recipeId)) return null;

            var made = recipes[recipeId]?.ProductID;

            if (made == null || made.Count == 0 || !items.ContainsKey(made[0])) return null;

            var key = items[made[0]]?.ItemName;
            var english = GameNames.Translate(config, key, LanguageType.English);
            var chinese = GameNames.Translate(config, key, LanguageType.Chinese) ?? items[made[0]]?.ItemName_Local;

            return english != null ? (english, string.IsNullOrWhiteSpace(chinese) ? english : chinese) : null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
