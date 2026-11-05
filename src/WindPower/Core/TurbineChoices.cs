using System;
using System.Collections.Generic;
using System.Linq;

namespace WindPower.Core;

/// <summary>
/// What one turbine is made of and gives back, from its settings: the materials used and why the player's could not
/// be, what taking it down gives back and why the player's list could not be used, and the Return percent share of
/// the materials, which a blank Returns gives back.
/// </summary>
public sealed record TurbineChoice(TurbineKind Kind, int[] Materials, MaterialProblem? MaterialsProblem, int[] Back, MaterialProblem? ReturnsProblem, int[] Share, int Percent);

/// <summary>
/// Settles every turbine's materials and returns at once, in the order of <see cref="TurbineRows.All"/>, for the table
/// rows and for the Mod Manager's questions alike, so both say the same.
/// </summary>
public static class TurbineChoices
{
    public const string MaterialsKey = "Materials";

    public const string ReturnsKey = "Returns";

    /// <param name="settings">Each turbine's settings, in the order of <see cref="TurbineRows.All"/>.</param>
    /// <param name="recipeUsing">The id of a stock recipe made of these materials in any order, or null.</param>
    /// <param name="recipeName">The name of what a stock recipe makes, in both languages, when it can be found.</param>
    public static IReadOnlyList<TurbineChoice> Of(IReadOnlyList<TurbineSettings> settings, Func<int, bool> isItem, Func<IReadOnlyList<int>, int?> recipeUsing, Func<int, (string English, string Chinese)?>? recipeName = null)
    {
        var chosen = new List<TurbineChoice>();

        for (var i = 0; i < TurbineRows.All.Count; i++)
        {
            var kind = TurbineRows.All[i];
            var wanted = settings[i];

            // The earlier turbines' materials as chosen, and the later ones' defaults, which each may fall back to.
            var taken = chosen.Select(done => (done.Kind, (IReadOnlyList<int>)done.Materials, false))
                .Concat(TurbineRows.All.Skip(i + 1).Select(later => (later, (IReadOnlyList<int>)MaterialList.Standard(later.Defaults.Materials), true)));
            var (materials, problem) = MaterialList.Choose(wanted.Materials, kind, isItem, recipeUsing, taken, recipeName);
            var (back, returnsProblem) = MaterialList.Returns(wanted.Returns, materials, wanted.ReturnPercent, isItem);

            chosen.Add(new TurbineChoice(kind, materials, problem, back, returnsProblem, MaterialList.GivenBack(materials, wanted.ReturnPercent), wanted.ReturnPercent));
        }

        return chosen;
    }

    /// <summary>Why the turbine cannot use the list in this setting, "English\nChinese", or "" when it can.</summary>
    public static string Problem(IReadOnlyList<TurbineChoice> choices, string section, string key) => Find(choices, section) is not { } choice ? "" : key switch
    {
        MaterialsKey => choice.MaterialsProblem?.Player ?? "",
        ReturnsKey => choice.ReturnsProblem?.Player ?? "",
        _ => "",
    };

    /// <summary>What a blank Returns gives back now, written like the setting.</summary>
    public static string Blank(IReadOnlyList<TurbineChoice> choices, string section, string key) =>
        key == ReturnsKey && Find(choices, section) is { } choice ? MaterialList.Pairs(choice.Share) : "";

    public static string BlankNote(IReadOnlyList<TurbineChoice> choices, string section, string key) =>
        key == ReturnsKey && Find(choices, section) is { } choice
            ? $"Follows Return percent: {choice.Percent}% of the materials.\n按「Return percent」返还材料的 {choice.Percent}%。"
            : "";

    private static TurbineChoice? Find(IReadOnlyList<TurbineChoice> choices, string section) => choices.FirstOrDefault(choice => choice.Kind.Section == section);
}
