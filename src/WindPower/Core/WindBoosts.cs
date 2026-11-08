using System;
using System.Collections.Generic;
using System.Linq;

namespace WindPower;

/// <summary>
/// Lets other mods make the turbines stronger or weaker, the way Power Manager's solar multiplier changes the solar
/// panels. A mod adds a multiplier by name; every turbine's rating is multiplied by all of them, so a live setting applies within a
/// second. Adding again under the same name replaces the
/// old one. The wind itself (its word and percent) does not change, only what the turbines make of it. A mod can also
/// give calm air (Heavy Smog) some wind with <see cref="AddCalm"/>; that one does change the wind. Wind Power
/// reads <see cref="Factor"/> and <see cref="CalmShare"/> once a second, with the weather. Other mods
/// find this class by name, so its namespace, name and the signatures of <see cref="Add"/>, <see cref="Remove"/>,
/// <see cref="AddCalm"/> and <see cref="RemoveCalm"/> must stay as they are.
/// </summary>
public static class WindBoosts
{
    private static readonly Dictionary<string, Func<float>> Sources = new();
    private static readonly Dictionary<string, Func<float>> CalmSources = new();

    /// <summary>A multiplier that throws stops counting, and this is told its source's name once.</summary>
    internal static Action<string> OnFailed = _ => { };

    public static void Add(string source, Func<float> multiplier) => Put(Sources, source, multiplier);

    public static void Remove(string source) => Take(Sources, source);

    /// <summary>
    /// Calm air gets the wind of a clear day times what <paramref name="share"/> returns: 0 leaves it still, 1 is a
    /// full clear day. Wind Power 0.9.4 or newer.
    /// </summary>
    public static void AddCalm(string source, Func<float> share) => Put(CalmSources, source, share);

    public static void RemoveCalm(string source) => Take(CalmSources, source);

    /// <summary>The largest calm share any mod gives, kept between 0 and 1; 0 with none. Skipped and dropped like a multiplier.</summary>
    public static float CalmShare()
    {
        var most = 0f;

        foreach (var value in Read(CalmSources)) most = Math.Max(most, value);

        return Math.Min(1f, most);
    }

    private static void Put(Dictionary<string, Func<float>> sources, string source, Func<float> value)
    {
        if (string.IsNullOrEmpty(source) || value == null) return;

        lock (sources) sources[source] = value;
    }

    private static void Take(Dictionary<string, Func<float>> sources, string source)
    {
        if (string.IsNullOrEmpty(source)) return;

        lock (sources) sources.Remove(source);
    }

    /// <summary>
    /// Every multiplier multiplied together; 1 with none. A negative value counts as 0, one that is not a number is
    /// skipped, and one that throws is dropped for good.
    /// </summary>
    public static float Factor()
    {
        var factor = 1f;

        foreach (var value in Read(Sources)) factor *= value;

        return factor;
    }

    /// <summary>Each source's value, negatives as 0, with any that is not a number skipped and any that throws dropped.</summary>
    private static List<float> Read(Dictionary<string, Func<float>> from)
    {
        var values = new List<float>();
        List<KeyValuePair<string, Func<float>>>? failed = null;
        KeyValuePair<string, Func<float>>[] sources;

        // The sources run on a copy, so one that adds or removes a source cannot break the read.
        lock (from) sources = from.ToArray();

        foreach (var pair in sources)
        {
            float value;

            try
            {
                value = pair.Value();
            }
            catch (Exception)
            {
                (failed ??= new List<KeyValuePair<string, Func<float>>>()).Add(pair);
                continue;
            }

            if (float.IsNaN(value) || float.IsInfinity(value)) continue;

            values.Add(Math.Max(0f, value));
        }

        if (failed == null) return values;

        // A source added again since the copy keeps its new value.
        lock (from)
            foreach (var pair in failed)
                if (from.TryGetValue(pair.Key, out var current) && current == pair.Value) from.Remove(pair.Key);

        foreach (var pair in failed) OnFailed(pair.Key);

        return values;
    }
}
