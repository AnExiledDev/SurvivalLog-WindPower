using System;
using System.Collections.Generic;
using System.Numerics;

namespace WindPower.Core;

/// <summary>The ids of one turbine's six rows: the recipe crafts the package item, which unpacks into the furniture.</summary>
public sealed record RowIds(int Item, int Furniture, int Electrical, int Recipe, int Action, int Effect);

/// <summary>What the player sets for one turbine. Materials is written as id:count pairs, "20004:4, 20104:2".</summary>
public sealed record TurbineSettings(float RatedPower, string Materials, int ShopPrice, int ShopStock);

/// <summary>
/// One turbine the mod adds: the solar panel it copies (its rows, its prefab and the slot it fits), its own ids, model
/// and text, how it is unlocked, and the settings it starts with.
/// </summary>
public sealed record TurbineKind
{
    /// <summary>The English name, for the log.</summary>
    public string Name { get; init; } = "";

    /// <summary>"Small", "Medium" or "Large": the middle of each text key, WindPower_Medium_Name.</summary>
    public string Size { get; init; } = "";

    public RowIds Ids { get; init; } = new(0, 0, 0, 0, 0, 0);

    /// <summary>The solar panel's rows each of ours is copied from.</summary>
    public RowIds Stock { get; init; } = new(0, 0, 0, 0, 0, 0);

    /// <summary>The prefab name the furniture row gives. The game has no such prefab: the mod builds it.</summary>
    public string ResName { get; init; } = "";

    /// <summary>The solar panel's prefab the model is built on, so it takes the same spot and size.</summary>
    public string StockPrefab { get; init; } = "";

    public string ModelFile { get; init; } = "";

    public string IconFile { get; init; } = "";

    /// <summary>The moving parts by name, each with the axis in its own space it turns about.</summary>
    public IReadOnlyDictionary<string, Vector3> Movers { get; init; } = new Dictionary<string, Vector3>();

    /// <summary>
    /// Whether the top swings round into gusts. A vertical-axis rotor takes the wind from every side, so it never
    /// turns to face one.
    /// </summary>
    public bool TurnsIntoGusts { get; init; }

    /// <summary>The rotor's top speed as a share of the large turbine's.</summary>
    public float SpinScale { get; init; } = 1f;

    /// <summary>The stock recipe that reveals this one: anyone shown that solar panel or battery is shown the turbine too.</summary>
    public int RevealedWith { get; init; }

    /// <summary>The reward sets that show recipes and show this one as well.</summary>
    public IReadOnlyList<int> RewardSets { get; init; } = Array.Empty<int>();

    /// <summary>The quest whose text gets <see cref="QuestLine"/> added at the end.</summary>
    public int Quest { get; init; }

    public Text QuestLine { get; init; } = new("", "", "");

    /// <summary>What the save's name field starts with while this turbine is saved as its solar panel.</summary>
    public string SaveMarker { get; init; } = "";

    public TurbineSettings Defaults { get; init; } = new(0f, "", 0, 0);

    /// <summary>The names and descriptions its rows point at.</summary>
    public IReadOnlyList<Text> Texts { get; init; } = Array.Empty<Text>();

    /// <summary>Where the web UI finds the icon, written the way the game writes its own (from a page two folders below WebUI).</summary>
    public string WebIcon => "../../" + TurbineRows.WebIconFolder + "/" + IconFile;

    public string Key(string part) => $"WindPower_{Size}_{part}";
}
