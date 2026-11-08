using System;
using System.Collections.Generic;

namespace WindPower;

/// <summary>
/// Lets other mods make the turbines stronger or weaker, the way Power Manager's solar multiplier changes the solar
/// panels. A mod adds a multiplier by name; every turbine's rating is multiplied by all of them, so a live setting applies within a
/// second. Adding again under the same name replaces the
/// old one. The wind itself (its word and percent) does not change, only what the turbines make of it. Wind Power
/// reads <see cref="Factor"/> once a second, with the weather. Other mods
/// find this class by name, so its namespace, name and the signatures of <see cref="Add"/> and
/// <see cref="Remove"/> must stay as they are.
/// </summary>
public static class WindBoosts
{
    private static readonly Dictionary<string, Func<float>> Sources = new();

    /// <summary>A multiplier that throws stops counting, and this is told its source's name once.</summary>
    internal static Action<string> OnFailed = _ => { };

    public static void Add(string source, Func<float> multiplier)
    {
        if (string.IsNullOrEmpty(source) || multiplier == null) return;

        lock (Sources) Sources[source] = multiplier;
    }

    public static void Remove(string source)
    {
        if (string.IsNullOrEmpty(source)) return;

        lock (Sources) Sources.Remove(source);
    }

    /// <summary>
    /// Every multiplier multiplied together; 1 with none. A negative value counts as 0, one that is not a number is
    /// skipped, and one that throws is dropped for good.
    /// </summary>
    public static float Factor()
    {
        var factor = 1f;
        List<string>? failed = null;

        lock (Sources)
        {
            foreach (var pair in Sources)
            {
                float value;

                try
                {
                    value = pair.Value();
                }
                catch (Exception)
                {
                    (failed ??= new List<string>()).Add(pair.Key);
                    continue;
                }

                if (float.IsNaN(value) || float.IsInfinity(value)) continue;

                factor *= Math.Max(0f, value);
            }

            if (failed != null)
                foreach (var name in failed) Sources.Remove(name);
        }

        if (failed != null)
            foreach (var name in failed) OnFailed(name);

        return factor;
    }
}
