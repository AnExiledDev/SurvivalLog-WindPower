using System;

namespace WindPower.Core;

/// <summary>
/// How one turbine moves, frame by frame. The rotor's speed follows the wind's share of the rating, eased so it winds
/// up over a few seconds and coasts down over longer. Every so often a gust comes through: the rotor picks up speed,
/// on the large turbine the whole top swings round to meet it, and both settle back as it passes. Gusts come at uneven times and from
/// either side, and each turbine has its own phase, so two turbines never move in step. All angles are in degrees;
/// the adapter turns each part to them.
/// </summary>
public sealed class TurbineMotion
{
    /// <summary>Degrees a second at full wind for the large turbine's rotor, a little under a turn.</summary>
    public const float MaxSpin = 340f;

    /// <summary>How far the steady flutter pushes the speed above and below the wind's own.</summary>
    public const float FlutterDepth = 0.05f;

    /// <summary>How much a full-strength gust adds to the speed at its peak.</summary>
    public const float GustBoost = 0.25f;

    /// <summary>How far the top swings round for a full-strength gust, at full wind.</summary>
    public const float GustYaw = 28f;

    /// <summary>Seconds between the starts of two gusts, before the uneven part.</summary>
    public const float GustEvery = 7f;

    /// <summary>How long one gust lasts, swell to calm.</summary>
    public const float GustLength = 3.5f;

    // Time constants: about 95% of the way after three of them, so a 4 s spin-up and an 8 s coast. The top's lag is
    // what makes it read as heavy: it trails the gust by about half a second.
    private const float SpinUpTau = 4f / 3f;
    private const float SpinDownTau = 8f / 3f;
    private const float YawTau = 0.45f;

    /// <summary>A frame longer than this (a load, a hitch) is taken as this long, so nothing jumps.</summary>
    private const float LongestStep = 0.1f;

    private readonly float _phase;
    private readonly float _maxSpin;
    private readonly bool _turnsIntoGusts;
    private float _level;
    private bool _hasStarted;

    /// <param name="phase">Seconds this turbine's gusts run ahead of the others'.</param>
    /// <param name="spinScale">The rotor's top speed as a share of <see cref="MaxSpin"/>.</param>
    /// <param name="turnsIntoGusts">False for a rotor that takes the wind from every side, whose top never swings.</param>
    public TurbineMotion(float phase, float spinScale = 1f, bool turnsIntoGusts = true)
    {
        _phase = phase;
        _maxSpin = MaxSpin * Math.Max(0f, spinScale);
        _turnsIntoGusts = turnsIntoGusts;
    }

    public float RotorAngle { get; private set; }

    /// <summary>How far the whole top (head, rotor and tail together) is turned off its rest.</summary>
    public float HeadYaw { get; private set; }

    /// <summary>The rotor's speed in degrees a second after the last step.</summary>
    public float Spin { get; private set; }

    /// <param name="seconds">The frame's length.</param>
    /// <param name="time">The game clock in seconds, which stops while the game is paused.</param>
    /// <param name="share">The wind's share of the rating, 0 to 1.</param>
    public void Step(float seconds, float time, float share)
    {
        var target = Math.Clamp(share, 0f, 1f);
        var dt = Math.Clamp(seconds, 0f, LongestStep);
        var gust = Gust(time + _phase);

        // A turbine that comes into view takes the weather as it is, instead of winding up from still every load.
        if (!_hasStarted)
        {
            _hasStarted = true;
            _level = target;
            HeadYaw = _turnsIntoGusts ? Yaw(gust, target) : 0f;
        }

        _level = Ease(_level, target, dt, target > _level ? SpinUpTau : SpinDownTau);

        Spin = _maxSpin * _level * (1f + FlutterDepth * Flutter(time + _phase) + GustBoost * Math.Abs(gust));
        RotorAngle = (RotorAngle + Spin * dt) % 360f;
        if (_turnsIntoGusts) HeadYaw = Ease(HeadYaw, Yaw(gust, _level), dt, YawTau);
    }

    /// <summary>
    /// The gust blowing at <paramref name="t"/>, between -1 and 1: 0 between gusts, and during one a smooth swell and
    /// fall whose sign is the side it comes from. Each gust has its own strength and side, and about one in five
    /// never comes, so they do not arrive like clockwork.
    /// </summary>
    public static float Gust(float t)
    {
        var index = MathF.Floor(t / GustEvery);
        var start = index * GustEvery + GustEvery * 0.4f * Noise(index, 1);
        var into = t - start;

        if (into < 0f || into > GustLength || Noise(index, 2) < 0.2f) return 0f;

        var swell = MathF.Sin(MathF.PI * into / GustLength);
        var strength = 0.55f + 0.45f * Noise(index, 3);
        var side = Noise(index, 4) < 0.5f ? -1f : 1f;

        return side * strength * swell * swell;
    }

    /// <summary>Small quick waves that never line up, between -1 and 1: the wind is never quite steady.</summary>
    public static float Flutter(float t) => (MathF.Sin(t * 1.7f) + 0.6f * MathF.Sin(t * 3.1f + 1.1f) + 0.4f * MathF.Sin(t * 0.53f + 2f)) / 2f;

    // A light wind barely moves the top; from half the rating on, gusts swing it the whole way.
    private static float Yaw(float gust, float level) => GustYaw * Math.Min(1f, level * 2f) * gust;

    /// <summary>A fixed number in [0, 1) for each gust and question, so a gust looks the same every time it is drawn.</summary>
    private static float Noise(float index, int salt)
    {
        var x = MathF.Sin(index * 12.9898f + salt * 78.233f) * 43758.5453f;

        return x - MathF.Floor(x);
    }

    private static float Ease(float from, float to, float dt, float tau) => from + (to - from) * (1f - MathF.Exp(-dt / tau));
}
