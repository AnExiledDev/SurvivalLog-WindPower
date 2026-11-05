using System;
using System.Collections.Generic;
using System.Linq;
using WindPower.Core;

namespace WindPower.Adapters;

/// <summary>
/// The settings each turbine runs on now, in the order of <see cref="TurbineRows.All"/>. A change replaces the whole
/// list, so a reader always sees one complete set and the table check sees a change by the list itself.
/// </summary>
internal static class WindSettings
{
    private static TurbineSettings[] _current = TurbineRows.All.Select(kind => kind.Defaults).ToArray();

    public static IReadOnlyList<TurbineSettings> Current => _current;

    /// <summary>How far the wind swings above and below the weather's average, in percent of it.</summary>
    public static int VariationPercent { get; set; } = WindVariation.DefaultPercent;

    public static TurbineSettings Of(TurbineKind kind) => _current[IndexOf(kind)];

    public static void Set(TurbineKind kind, TurbineSettings settings)
    {
        var next = (TurbineSettings[])_current.Clone();

        next[IndexOf(kind)] = settings;
        _current = next;
    }

    private static int IndexOf(TurbineKind kind)
    {
        for (var i = 0; i < TurbineRows.All.Count; i++)
            if (ReferenceEquals(TurbineRows.All[i], kind)) return i;

        throw new ArgumentException(kind.Name + " is not one of the mod's turbines", nameof(kind));
    }
}
