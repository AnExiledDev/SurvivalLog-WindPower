using WindPower.Core;
using Xunit;

namespace WindPower.Tests;

public class TurbineMotionTests
{
    private const float Frame = 1f / 60f;

    // The fastest the rotor may turn: full wind, the flutter at its top and a full-strength gust at its peak.
    private const float Fastest = TurbineMotion.MaxSpin * (1f + TurbineMotion.FlutterDepth + TurbineMotion.GustBoost);

    /// <summary>Runs the turbine for <paramref name="seconds"/> of frames, the clock going on from <paramref name="from"/>.</summary>
    private static float Run(TurbineMotion motion, float from, float seconds, float share)
    {
        var time = from;

        for (var t = 0f; t < seconds; t += Frame)
        {
            time += Frame;
            motion.Step(Frame, time, share);
        }

        return time;
    }

    /// <summary>The widest the top swings either way, and the rotor's slowest and fastest, over a run.</summary>
    private static (float Left, float Right, float Slowest, float Fastest) Watch(TurbineMotion motion, float seconds, float share)
    {
        var (left, right, slowest, fastest) = (0f, 0f, float.MaxValue, 0f);
        var time = 0f;

        for (var i = 0; i < seconds * 60; i++)
        {
            time += Frame;
            motion.Step(Frame, time, share);
            left = Math.Min(left, motion.HeadYaw);
            right = Math.Max(right, motion.HeadYaw);
            slowest = Math.Min(slowest, motion.Spin);
            fastest = Math.Max(fastest, motion.Spin);
        }

        return (left, right, slowest, fastest);
    }

    [Fact]
    public void ATurbineComingIntoViewAlreadyTurnsAtTheWeathersPace()
    {
        var motion = new TurbineMotion(0f);

        motion.Step(Frame, 0f, 0.5f);

        Assert.InRange(motion.Spin, 0.5f * TurbineMotion.MaxSpin * (1 - TurbineMotion.FlutterDepth), 0.5f * Fastest);
    }

    [Fact]
    public void InSmogTheRotorAndTopStandStill()
    {
        var motion = new TurbineMotion(3f);
        var end = Run(motion, 0f, 1f, 0f);
        var angle = motion.RotorAngle;

        Run(motion, end, 30f, 0f);

        Assert.Equal(0f, motion.Spin);
        Assert.Equal(angle, motion.RotorAngle);
        Assert.Equal(0f, motion.HeadYaw, 3);
    }

    // Winding up takes a few seconds and coasting down takes longer, the way a heavy rotor behaves.
    [Fact]
    public void TheRotorWindsUpInAboutFourSecondsAndCoastsDownMoreSlowly()
    {
        var rising = new TurbineMotion(0f);
        var falling = new TurbineMotion(0f);

        rising.Step(Frame, 0f, 0f);
        falling.Step(Frame, 0f, 1f);
        Run(rising, 0f, 1f, 1f);
        Run(falling, 0f, 1f, 0f);

        // Spin over the rotor's level, so a gust in this second does not change the reading.
        var risen = rising.Spin / Fastest;
        var stillTurning = falling.Spin / (TurbineMotion.MaxSpin * (1f - TurbineMotion.FlutterDepth));

        Assert.InRange(risen, 0.15f, 0.75f);
        Assert.InRange(stillTurning, 0.5f, 1f);

        Run(rising, 1f, 5f, 1f);

        Assert.InRange(rising.Spin, TurbineMotion.MaxSpin * (1f - TurbineMotion.FlutterDepth) * 0.95f, Fastest);
    }

    // The blades turn a little faster at full wind than the 270 degrees a second they used to.
    [Fact]
    public void AtFullWindTheBladesTurnFasterThanBefore()
    {
        var motion = new TurbineMotion(0f);

        motion.Step(Frame, 0f, 1f);

        Assert.True(TurbineMotion.MaxSpin > 270f);
        Assert.InRange(motion.Spin, 300f, Fastest);
    }

    // Gusts are what move the top: within a minute of steady wind it swings well round, both ways.
    [Fact]
    public void GustsSwingTheWholeTopRoundAndBothWays()
    {
        var (left, right, _, _) = Watch(new TurbineMotion(0f), 120f, 1f);

        Assert.True(right > 10f, $"the top only swung {right} degrees one way");
        Assert.True(left < -10f, $"the top only swung {left} degrees the other way");
        Assert.InRange(Math.Max(right, -left), 10f, TurbineMotion.GustYaw);
    }

    // Between gusts the top settles back to rest instead of wandering.
    [Fact]
    public void BetweenGustsTheTopSettlesBack()
    {
        var motion = new TurbineMotion(0f);
        var time = 0f;
        var settled = false;

        for (var i = 0; i < 60 * 60 && !settled; i++)
        {
            time += Frame;
            motion.Step(Frame, time, 1f);
            settled = TurbineMotion.Gust(time) == 0f && Math.Abs(motion.HeadYaw) < 0.5f && i > 60 * 10;
        }

        Assert.True(settled, "the top never came back to rest between gusts in a minute");
    }

    [Fact]
    public void AGustSpeedsTheRotorUp()
    {
        var (_, _, slowest, fastest) = Watch(new TurbineMotion(0f), 120f, 1f);

        Assert.True(fastest > TurbineMotion.MaxSpin * 1.15f, $"gusts never sped the rotor past {fastest}");
        Assert.InRange(fastest, 0f, Fastest + 0.01f);
        Assert.InRange(slowest, TurbineMotion.MaxSpin * (1f - TurbineMotion.FlutterDepth) - 0.01f, TurbineMotion.MaxSpin);
    }

    // A light wind barely moves the top, so a Clear day does not look stormy.
    [Fact]
    public void ALightWindSwingsTheTopLess()
    {
        var (left, right, _, _) = Watch(new TurbineMotion(0f), 120f, 0.12f);

        Assert.InRange(Math.Max(right, -left), 0.5f, TurbineMotion.GustYaw * 0.3f);
    }

    // Gusts must not arrive like clockwork: the gaps between them differ.
    [Fact]
    public void GustsComeAtUnevenTimes()
    {
        var starts = new List<float>();
        var wasBlowing = false;

        for (var t = 0f; t < 300f; t += Frame)
        {
            var isBlowing = TurbineMotion.Gust(t) != 0f;

            if (isBlowing && !wasBlowing) starts.Add(t);

            wasBlowing = isBlowing;
        }

        var gaps = starts.Zip(starts.Skip(1), (a, b) => b - a).ToList();

        Assert.True(starts.Count >= 25, $"only {starts.Count} gusts in five minutes");
        Assert.True(gaps.Max() - gaps.Min() > 2f, "every gap between gusts is the same length");
    }

    // Now and then a gust never comes, so the rhythm breaks with a longer calm.
    [Fact]
    public void SomeGustsNeverCome()
    {
        var calm = 0;

        for (var window = 0; window < 200; window++)
        {
            var from = window * TurbineMotion.GustEvery;
            var blows = false;

            for (var t = from; t < from + TurbineMotion.GustEvery && !blows; t += 0.05f) blows = TurbineMotion.Gust(t) != 0f;

            if (!blows) calm++;
        }

        Assert.InRange(calm, 15, 70);
    }

    [Fact]
    public void TwoTurbinesWithDifferentPhasesDoNotMoveInStep()
    {
        var one = new TurbineMotion(0f);
        var two = new TurbineMotion(250f);

        Run(one, 0f, 30f, 0.6f);
        Run(two, 0f, 30f, 0.6f);

        Assert.NotEqual(one.Spin, two.Spin);
        Assert.NotEqual(one.HeadYaw, two.HeadYaw);
    }

    // The medium turbine's rotor stands upright and takes the wind from every side, so its top never swings round,
    // though a gust still speeds the rotor up.
    [Fact]
    public void ARotorThatTakesTheWindFromEverySideNeverSwingsItsTop()
    {
        var (left, right, _, fastest) = Watch(new TurbineMotion(0f, 0.6f, turnsIntoGusts: false), 120f, 1f);

        Assert.Equal(0f, left);
        Assert.Equal(0f, right);
        Assert.True(fastest > 0.6f * TurbineMotion.MaxSpin * 1.15f, $"gusts never sped the rotor past {fastest}");
    }

    [Fact]
    public void ASmallerRotorTurnsAtItsShareOfTheLargeOnesSpeed()
    {
        var large = new TurbineMotion(5f);
        var medium = new TurbineMotion(5f, 0.6f, turnsIntoGusts: false);

        Run(large, 0f, 20f, 0.7f);
        Run(medium, 0f, 20f, 0.7f);

        Assert.InRange(medium.Spin / large.Spin, 0.599f, 0.601f);
    }

    // A load or a hitch hands over one huge frame; the rotor must not leap round by it.
    [Fact]
    public void ALongFrameDoesNotMakeTheRotorJump()
    {
        var motion = new TurbineMotion(0f);

        motion.Step(Frame, 0f, 1f);

        var before = motion.RotorAngle;

        motion.Step(5f, 5f, 1f);

        var turned = (motion.RotorAngle - before + 360f) % 360f;

        Assert.InRange(turned, 0f, Fastest * 0.1f + 0.01f);
    }
}
