using System;

namespace WindPower.Core;

/// <summary>
/// How the wind rises and falls within one weather. The weather sets the wind's average (<see cref="WindOutput.Share"/>);
/// on top of it the wind swells and slackens over the hours: a slow swing over most of a day and a quicker one over an
/// hour or two. Both come from the game clock alone, so every turbine feels the same wind, a save loads into the same
/// wind it left, and over a long spell the average stays the weather's own. Still air (Heavy Smog) stays still.
/// </summary>
public static class WindVariation
{
    /// <summary>How far the wind swings above and below the weather's average by default, as a share of it.</summary>
    public const int DefaultPercent = 30;

    /// <summary>The widest swing the setting allows; past it a calm spell would stop the turbines in any weather.</summary>
    public const int MostPercent = 60;

    // The slow swing carries most of the change, the quick one adds the hour-to-hour unevenness.
    private const double SlowSeconds = 6 * 3600;
    private const double QuickSeconds = 1.5 * 3600;
    private const float SlowWeight = 0.7f;
    private const float QuickWeight = 0.3f;

    /// <summary>The wind's share of the rating at <paramref name="gameSeconds"/>: the weather's share pushed up or down by up to <paramref name="percent"/>%.</summary>
    public static float Share(float weatherShare, int percent, double gameSeconds)
    {
        var depth = Math.Clamp(percent, 0, MostPercent) / 100f;

        return Math.Max(0f, weatherShare * (1f + depth * Swing(gameSeconds)));
    }

    /// <summary>Where the wind stands in its swing at <paramref name="gameSeconds"/>, between -1 (calmest) and 1 (strongest). Averages to 0.</summary>
    public static float Swing(double gameSeconds) => SlowWeight * Wave(gameSeconds / SlowSeconds, 1) + QuickWeight * Wave(gameSeconds / QuickSeconds, 2);

    /// <summary>A smooth curve through a fixed random value between -1 and 1 at each whole step.</summary>
    private static float Wave(double steps, uint salt)
    {
        var index = Math.Floor(steps);
        var into = (float)(steps - index);
        var eased = into * into * (3f - 2f * into);
        var from = Knot((long)index, salt);
        var to = Knot((long)index + 1, salt);

        return from + (to - from) * eased;
    }

    /// <summary>A fixed value between -1 and 1 for each step, the same every time it is asked for.</summary>
    private static float Knot(long index, uint salt)
    {
        unchecked
        {
            var x = (uint)index * 0x9E3779B1u ^ (uint)(index >> 32) * 0x85EBCA77u ^ salt * 0xC2B2AE3Du;

            x ^= x >> 16;
            x *= 0x7FEB352Du;
            x ^= x >> 15;
            x *= 0x846CA68Bu;
            x ^= x >> 16;

            return (x / (float)uint.MaxValue) * 2f - 1f;
        }
    }
}
