using System;
using System.Collections.Generic;
using System.Linq;

namespace WindPower.Core;

/// <summary>One weather's wind setting: the Config_Weather ids it covers and its default percent of full wind.</summary>
public sealed record WeatherWind(string Key, string Name, string Chinese, int[] Ids, int DefaultPercent);

/// <summary>
/// The wind each weather brings, as settings in percent of full wind (a turbine's rating). None in smog (smog forms in
/// still air), a light breeze on clear days, more as fronts bring rain, and the most in cold snaps. Heavy Rain stops
/// short of full, as a storm's wind is gusty rather than strong. Weathers that differ only in rain share one setting.
/// </summary>
public static class WeatherWinds
{
    /// <summary>The highest a weather's wind can be set: twice full wind.</summary>
    public const int MostPercent = 200;

    public static readonly IReadOnlyList<WeatherWind> All = new[]
    {
        new WeatherWind("HeavySmog", "Heavy Smog", "「大雾霾天」", new[] { 12 }, 0),
        new WeatherWind("ColdClear", "Frosty/Clear, Cold/Clear and Chilly/Clear", "「霜晴」「寒晴」和「凛晴」", new[] { 7, 8, 9 }, 10),
        new WeatherWind("Clear", "Clear", "「晴天」", new[] { 1 }, 12),
        new WeatherWind("Cloudy", "Cloudy", "「阴天」", new[] { 2 }, 20),
        new WeatherWind("Rainy", "Rainy", "「雨天」", new[] { 3 }, 35),
        new WeatherWind("ContinuousRain", "Continuous Rain", "「持续阴雨」", new[] { 11, 14 }, 45),
        new WeatherWind("MildCold", "Mild Cold and Mild Cold Rain", "「轻度寒潮」和「轻度寒雨」", new[] { 4, 15 }, 55),
        new WeatherWind("HeavyRain", "Heavy Rain", "「暴雨」", new[] { 10, 13 }, 75),
        new WeatherWind("ModerateCold", "Moderate Cold and Moderate Cold Rain", "「中度寒潮」和「中度寒雨」", new[] { 5, 16 }, 80),
        new WeatherWind("SevereCold", "Severe Cold and Severe Cold Rain", "「重度寒潮」和「重度寒雨」", new[] { 6, 17 }, 100),
    };

    /// <summary>Every weather's default percent, in the order of <see cref="All"/>.</summary>
    public static readonly IReadOnlyList<int> Defaults = All.Select(weather => weather.DefaultPercent).ToArray();

    /// <summary>A weather the list does not know (one a game update adds) gets a cloudy day's wind.</summary>
    public static readonly int UnknownIndex = IndexOf("Cloudy");

    public static readonly int ClearIndex = IndexOf("Clear");

    /// <summary>Heavy Smog, the one weather another mod's smog setting gives wind to.</summary>
    public static readonly int SmogIndex = IndexOf("HeavySmog");

    /// <summary>Where the weather's setting sits in <see cref="All"/>; a weather the list does not know counts as cloudy.</summary>
    public static int IndexOfWeather(int weatherId)
    {
        for (var i = 0; i < All.Count; i++)
            if (Array.IndexOf(All[i].Ids, weatherId) >= 0) return i;

        return UnknownIndex;
    }

    private static int IndexOf(string key)
    {
        for (var i = 0; i < All.Count; i++)
            if (All[i].Key == key) return i;

        throw new ArgumentException(key);
    }
}
