using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace WindPower.Core;

/// <summary>Why a list from the settings cannot be used: a line for the log, and the reason for the player in both languages.</summary>
public sealed record MaterialProblem(string Log, string English, string Chinese)
{
    /// <summary>"English\nChinese", the way the Mod Manager reads a reason.</summary>
    public string Player => English + "\n" + Chinese;
}

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
    /// stock recipe's, or one <paramref name="taken"/> by another turbine), since the game files recipes by their
    /// materials and only one of two alike could be crafted.
    /// </summary>
    /// <param name="taken">Each other turbine's materials, and whether they are its default, kept for it in case it falls back.</param>
    /// <param name="recipeName">The name of what a stock recipe makes, in both languages, when it can be found.</param>
    public static (int[] Materials, MaterialProblem? Problem) Choose(string? text, TurbineKind kind, Func<int, bool> isItem, Func<IReadOnlyList<int>, int?> recipeUsing, IEnumerable<(TurbineKind Kind, IReadOnlyList<int> Materials, bool IsDefault)> taken, Func<int, (string English, string Chinese)?>? recipeName = null)
    {
        var standard = Standard(kind.Defaults.Materials);
        const string keeps = "so the turbine keeps its default materials.";
        const string keepsZh = "风力发电机会改用默认材料。";
        const string noSharing = "Two recipes cannot take the same materials, so this turbine keeps its default materials.";
        const string noSharingZh = "两个配方不能用完全相同的材料，这台风力发电机会改用默认材料。";

        MaterialProblem Problem(string log, string english, string chinese) => new($"the materials '{text}' cannot be used: {log}", english, chinese);

        if (string.IsNullOrWhiteSpace(text)) return (standard, Problem("it lists no materials", "The list is empty, " + keeps, "材料清单是空的，" + keepsZh));

        if (!TryParse(text, out var materials, out var problem)) return (standard, Problem(problem, $"The list cannot be read ({problem}), " + keeps, "这份清单无法读取，" + keepsZh));

        if (materials.Distinct().FirstOrDefault(id => !isItem(id)) is var missing and > 0) return (standard, Problem($"the game has no item {missing}", $"The game has no item {missing}, " + keeps, $"游戏里没有物品 {missing}，" + keepsZh));

        if (recipeUsing(materials) is int other)
        {
            var made = recipeName?.Invoke(other);
            var english = made is { } name ? $"The game's own {name.English} recipe takes exactly these materials. " : "A recipe the game already has takes exactly these materials. ";
            var chinese = made is { } same ? $"游戏自带的{same.Chinese}配方用的正是这些材料。" : "游戏里已有配方用的正是这些材料。";

            return (standard, Problem($"recipe {other} already uses them", english + noSharing, chinese + noSharingZh));
        }

        var key = TurbineRows.MaterialKey(materials);

        foreach (var (turbine, list, isDefault) in taken)
        {
            if (TurbineRows.MaterialKey(list) != key) continue;

            return isDefault
                ? (standard, Problem($"they are the {turbine.Name}'s default materials", $"These are the {turbine.Name}'s default materials, which it keeps in case its own list cannot be used. " + noSharing, $"这是{turbine.ChineseName}的默认材料，要留给它备用。" + noSharingZh))
                : (standard, Problem($"the {turbine.Name} already uses them", $"The {turbine.Name} already takes exactly these materials. " + noSharing, $"{turbine.ChineseName}已经用了完全相同的材料。" + noSharingZh));
        }

        return (materials, null);
    }

    /// <summary>The word a Returns list holds when taking the turbine down gives nothing back.</summary>
    public const string NoItems = "none";

    /// <summary>
    /// What taking a turbine made of <paramref name="materials"/> down gives back: <paramref name="percent"/> of them
    /// when <paramref name="text"/> is blank, nothing for "none", else the list as written. A list that cannot be used
    /// gives back the percent share, with why.
    /// </summary>
    public static (int[] Back, MaterialProblem? Problem) Returns(string? text, IReadOnlyList<int> materials, int percent, Func<int, bool> isItem)
    {
        var share = GivenBack(materials, percent);
        const string falls = "so taking the turbine down gives back its Return percent share.";
        const string fallsZh = "拆除时改按「Return percent」返还。";

        if (string.IsNullOrWhiteSpace(text)) return (share, null);

        if (text.Trim().Equals(NoItems, StringComparison.OrdinalIgnoreCase)) return (Array.Empty<int>(), null);

        if (!TryParse(text, out var back, out var problem)) return (share, new MaterialProblem($"the returns '{text}' cannot be used: {problem}", $"The list cannot be read ({problem}), " + falls, "这份清单无法读取，" + fallsZh));

        if (back.Distinct().FirstOrDefault(id => !isItem(id)) is var missing and > 0) return (share, new MaterialProblem($"the returns '{text}' cannot be used: the game has no item {missing}", $"The game has no item {missing}, " + falls, $"游戏里没有物品 {missing}，" + fallsZh));

        return (back, null);
    }

    /// <summary>Pieces written back the way the settings hold them, "20004:3,20104:2", in the order they first appear.</summary>
    public static string Pairs(IEnumerable<int> pieces) => string.Join(",", pieces.GroupBy(id => id).Select(group => group.Key.ToString(CultureInfo.InvariantCulture) + ":" + group.Count().ToString(CultureInfo.InvariantCulture)));

    /// <summary>The share of its materials a turbine gives back when taken down by default, about what the game's own furniture gives.</summary>
    public const int DefaultReturnPercent = 50;

    /// <summary>
    /// What taking a turbine down gives back: <paramref name="percent"/> of what it was made from. Each material gives
    /// back its share rounded down, then ones whose share was rounded down give one more, in the order listed, until
    /// that share of all the pieces (at least one, unless the share is 0) is back. No material comes back more than it
    /// went in.
    /// </summary>
    public static int[] GivenBack(IReadOnlyList<int> materials, int percent = DefaultReturnPercent)
    {
        var share = Math.Clamp(percent, 0, 100);
        var order = new List<int>();
        var counts = new Dictionary<int, int>();

        foreach (var id in materials)
        {
            if (!counts.ContainsKey(id)) order.Add(id);

            counts[id] = counts.TryGetValue(id, out var had) ? had + 1 : 1;
        }

        var back = order.ToDictionary(id => id, id => counts[id] * share / 100);
        var wanted = Math.Max(1, materials.Count * share / 100);
        var total = back.Values.Sum();

        foreach (var id in order)
        {
            if (total >= wanted) break;

            if (counts[id] * share % 100 == 0) continue;

            back[id]++;
            total++;
        }

        return order.SelectMany(id => Enumerable.Repeat(id, back[id])).ToArray();
    }

    /// <summary>A turbine's default materials, which the mod itself writes, so one that does not parse is the mod's own mistake.</summary>
    public static int[] Standard(string text) => TryParse(text, out var materials, out var problem) ? materials : throw new ArgumentException($"the default materials '{text}' do not parse: {problem}", nameof(text));
}
