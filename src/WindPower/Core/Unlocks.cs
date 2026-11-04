using System;
using System.Collections.Generic;
using System.Linq;

namespace WindPower.Core;

/// <summary>
/// The edits to the game's own rows that bring the turbines into a run: the quest rewards that show recipes, the
/// pre-disaster shops' stock lists, and the quest text. Each edit can run again on rows it already changed and leaves
/// them as they are.
/// </summary>
public static class Unlocks
{
    /// <summary>The reward's recipe list with each of <paramref name="recipes"/> added at the end once, or null when it already has them all.</summary>
    public static int[]? WithRecipes(IReadOnlyList<int> list, IEnumerable<int> recipes)
    {
        var missing = recipes.Where(id => !list.Contains(id)).Distinct().ToList();

        return missing.Count == 0 ? null : list.Concat(missing).ToArray();
    }

    /// <summary>
    /// A shop's stock lists, the furniture ids and how many of each, with every one of <paramref name="ours"/> taken
    /// out and then each of <paramref name="wanted"/> with a count above 0 added at the end. Null when that changes
    /// nothing. A turbine with no stock is left out, so the shop does not list it at all.
    /// </summary>
    public static (int[] Ids, int[] Counts)? Shop(IReadOnlyList<int> ids, IReadOnlyList<int> counts, IReadOnlyCollection<int> ours, IReadOnlyList<(int Id, int Count)> wanted)
    {
        var keptIds = new List<int>();
        var keptCounts = new List<int>();

        for (var i = 0; i < ids.Count; i++)
        {
            if (ours.Contains(ids[i])) continue;

            keptIds.Add(ids[i]);
            keptCounts.Add(i < counts.Count ? counts[i] : 0);
        }

        foreach (var (id, count) in wanted.Where(pair => pair.Count > 0))
        {
            keptIds.Add(id);
            keptCounts.Add(count);
        }

        var isSame = keptIds.SequenceEqual(ids) && keptCounts.SequenceEqual(counts.Take(ids.Count)) && counts.Count == ids.Count;

        return isSame ? null : (keptIds.ToArray(), keptCounts.ToArray());
    }

    /// <summary>
    /// The quest text with <paramref name="line"/> added as a paragraph of its own, or null when it is already there.
    /// The language tables hold real line breaks and the rows' own text holds the two characters \n, so the caller
    /// says which.
    /// </summary>
    public static string? WithLine(string? text, string line, string lineBreak)
    {
        if (string.IsNullOrEmpty(text)) return line;

        return text!.Contains(line) ? null : text + lineBreak + line;
    }

    /// <summary>
    /// A live shop's count of one turbine after the stock setting goes from <paramref name="before"/> to
    /// <paramref name="after"/>: moved by the difference, so ones already bought stay bought, never below none and
    /// never above a full shop.
    /// </summary>
    public static int NewStock(int current, int before, int after) => Math.Clamp(current + after - before, 0, Math.Max(0, after));
}
