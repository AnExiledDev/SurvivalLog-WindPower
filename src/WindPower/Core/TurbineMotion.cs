using System;

namespace WindPower.Core;

/// <summary>
/// How one turbine moves, frame by frame. The rotor's speed follows the wind's share of the rating, eased so it winds
/// up over a few seconds and coasts down over longer, and breathes with small gusts so two turbines never turn in step.
/// The head hunts a little about the mast as the wind shifts, and the tail swings side-on in a storm. All angles are in
/// degrees; the adapter turns each part to them.
/// </summary>
public sealed class TurbineMotion
{
    /// <summary>Degrees a second at full wind: three-quarters of a turn, a heavy rotor's pace.</summary>
    public const float MaxSpin = 270f;

    /// <summary>How far the gusts push the speed above and below the wind's own.</summary>
    public const float GustDepth = 0.15f;

    public const float FurlAngle = 70f;

    /// <summary>How far the head hunts either side of its rest, at full wind.</summary>
    public const float HuntDegrees = 4.5f;

    // Time constants: about 95% of the way after three of them, so a 4 s spin-up, an 8 s coast and a 3 s furl.
    private const float SpinUpTau = 4f / 3f;
    private const float SpinDownTau = 8f / 3f;
    private const float FurlTau = 1f;

    /// <summary>A frame longer than this (a load, a hitch) is taken as this long, so nothing jumps.</summary>
    private const float LongestStep = 0.1f;

    private readonly float _phase;
    private float _level;
    private float _furl;
    private bool _hasStarted;

    /// <param name="phase">Seconds this turbine's gusts and hunting run ahead of the others'.</param>
    public TurbineMotion(float phase)
    {
        _phase = phase;
    }

    public float RotorAngle { get; private set; }

    public float HeadYaw { get; private set; }

    public float TailAngle { get; private set; }

    /// <summary>The rotor's speed in degrees a second after the last step.</summary>
    public float Spin { get; private set; }

    /// <param name="seconds">The frame's length.</param>
    /// <param name="time">The game clock in seconds, which stops while the game is paused.</param>
    /// <param name="share">The wind's share of the rating, 0 to 1.</param>
    /// <param name="isFurled">Whether the tail should be side-on.</param>
    public void Step(float seconds, float time, float share, bool isFurled)
    {
        var target = Math.Clamp(share, 0f, 1f);
        var furlTarget = isFurled ? 1f : 0f;
        var dt = Math.Clamp(seconds, 0f, LongestStep);

        // A turbine that comes into view takes the weather as it is, instead of winding up from still every load.
        if (!_hasStarted)
        {
            _hasStarted = true;
            _level = target;
            _furl = furlTarget;
        }

        _level = Ease(_level, target, dt, target > _level ? SpinUpTau : SpinDownTau);
        _furl = Ease(_furl, furlTarget, dt, FurlTau);

        var t = time + _phase;

        Spin = MaxSpin * _level * (1f + GustDepth * Gust(t));
        RotorAngle = (RotorAngle + Spin * dt) % 360f;
        HeadYaw = HuntDegrees * Math.Min(1f, _level * 2f) * Hunt(t);
        TailAngle = FurlAngle * Smooth(_furl);
    }

    /// <summary>Three slow waves that never line up, between -1 and 1: the wind's gusts.</summary>
    public static float Gust(float t) => (MathF.Sin(t * 0.9f) + 0.6f * MathF.Sin(t * 2.3f + 1.1f) + 0.4f * MathF.Sin(t * 0.37f + 2f)) / 2f;

    /// <summary>The head's drift as the wind backs and veers, between -1 and 1.</summary>
    public static float Hunt(float t) => (0.75f * MathF.Sin(t * 0.23f) + 0.25f * MathF.Sin(t * 0.71f + 1.3f));

    private static float Ease(float from, float to, float dt, float tau) => from + (to - from) * (1f - MathF.Exp(-dt / tau));

    private static float Smooth(float x) => x * x * (3f - 2f * x);
}
