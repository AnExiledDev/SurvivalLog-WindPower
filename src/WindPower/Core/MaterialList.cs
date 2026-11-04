using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace WindPower.Core;

/// <summary>
/// A recipe's materials as the player writes them in the settings, id:count pairs ("20004:4, 20104:2"), and as the
/// recipe table holds them, each id once per piece.
/// </summary>
public static class MaterialList
{
    /// <summary>The most different materials a stock recipe asks for (1.1.20151), so the crafting menu has room for them.</summary>
    public const int MostKinds = 7;

    public const int MostOfOne = 99;

    /// <summary>The materials in <paramref name="text"/>, or why it cannot be read.</summary>
    public static bool TryParse(string? text, out int[] materials, out string problem)
    {
        materials = Array.Empty<int>();
        problem = "";

        var pairs = (text ?? "").Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(pair => pair.Trim()).Where(pair => pair.Length > 0).ToList();

        if (pairs.Count == 0)
        {
            problem = "it lists no materials";

            return false;
        }

        var counts = new Dictionary<int, int>();
        var order = new List<int>();

        foreach (var pair in pairs)
        {
            var parts = pair.Split(':');

            if (parts.Length != 2 || !int.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) || !int.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var count))
            {
                problem = $"'{pair}' is not written as item id:count";

                return false;
            }

            if (id <= 0 || count < 1 || count > MostOfOne)
            {
                problem = $"'{pair}' needs an item id above 0 and a count from 1 to {MostOfOne}";

                return false;
            }

            if (!counts.ContainsKey(id)) order.Add(id);

            counts[id] = counts.TryGetValue(id, out var had) ? had + count : count;

            if (counts[id] > MostOfOne)
            {
                problem = $"item {id} is listed {counts[id]} times, more than {MostOfOne}";

                return false;
            }
        }

        if (order.Count > MostKinds)
        {
            problem = $"it lists {order.Count} different materials, more than the {MostKinds} a recipe can show";

            return false;
        }

        materials = order.SelectMany(id => Enumerable.Repeat(id, counts[id])).ToArray();

        return true;
    }

    /// <summary>
    /// The materials to use: the player's, or the default when theirs cannot be used, with why. Theirs cannot be used
    /// when the text cannot be read, names an item the game does not have, or matches another recipe's materials (a
    /// stock recipe's, or one already <paramref name="taken"/> by another turbine), since the game files recipes by
    /// their materials and only one of two alike could be crafted.
    /// </summary>
    public static (int[] Materials, string? Problem) Choose(string? text, string fallback, Func<int, bool> isItem, Func<IReadOnlyList<int>, int?> recipeUsing, IEnumerable<IReadOnlyList<int>> taken)
    {
        var standard = Standard(fallback);

        if (!TryParse(text, out var materials, out var problem)) return (standard, $"the materials '{text}' cannot be used: {problem}");

        if (materials.Distinct().FirstOrDefault(id => !isItem(id)) is var missing and > 0) return (standard, $"the materials '{text}' cannot be used: the game has no item {missing}");

        if (recipeUsing(materials) is int other) return (standard, $"the materials '{text}' cannot be used: recipe {other} already uses them");

        var key = TurbineRows.MaterialKey(materials);

        if (taken.Any(list => TurbineRows.MaterialKey(list) == key)) return (standard, $"the materials '{text}' cannot be used: another wind turbine already uses them");

        return (materials, null);
    }

    /// <summary>
    /// What taking a turbine down gives back: about half of what it was made from, like the game's own furniture. Each
    /// material gives back half its count rounded down, then ones with an odd count give one more, in the order
    /// listed, until half the pieces (at least one) are back. No material comes back more than it went in.
    /// </summary>
    public static int[] GivenBack(IReadOnlyList<int> materials)
    {
        var order = new List<int>();
        var counts = new Dictionary<int, int>();

        foreach (var id in materials)
        {
            if (!counts.ContainsKey(id)) order.Add(id);

            counts[id] = counts.TryGetValue(id, out var had) ? had + 1 : 1;
        }

        var back = order.ToDictionary(id => id, id => counts[id] / 2);
        var wanted = Math.Max(1, materials.Count / 2);
        var total = back.Values.Sum();

        foreach (var id in order)
        {
            if (total >= wanted) break;

            if (counts[id] % 2 == 0) continue;

            back[id]++;
            total++;
        }

        return order.SelectMany(id => Enumerable.Repeat(id, back[id])).ToArray();
    }

    /// <summary>A turbine's default materials, which the mod itself writes, so one that does not parse is the mod's own mistake.</summary>
    public static int[] Standard(string text) => TryParse(text, out var materials, out var problem) ? materials : throw new ArgumentException($"the default materials '{text}' do not parse: {problem}", nameof(text));
}
