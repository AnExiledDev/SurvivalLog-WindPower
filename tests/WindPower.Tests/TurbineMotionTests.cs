using WindPower.Core;
using Xunit;

namespace WindPower.Tests;

public class TurbineMotionTests
{
    private const float Frame = 1f / 60f;

    /// <summary>Runs the turbine for <paramref name="seconds"/> of frames, the clock going on from <paramref name="from"/>.</summary>
    private static float Run(TurbineMotion motion, float from, float seconds, float share, bool isFurled = false)
    {
        var time = from;

        for (var t = 0f; t < seconds; t += Frame)
        {
            time += Frame;
            motion.Step(Frame, time, share, isFurled);
        }

        return time;
    }

    [Fact]
    public void ATurbineComingIntoViewAlreadyTurnsAtTheWeathersPace()
    {
        var motion = new TurbineMotion(0f);

        motion.Step(Frame, 0f, 0.5f, false);

        Assert.InRange(motion.Spin, 0.5f * TurbineMotion.MaxSpin * (1 - TurbineMotion.GustDepth), 0.5f * TurbineMotion.MaxSpin * (1 + TurbineMotion.GustDepth));
    }

    [Fact]
    public void InSmogTheRotorAndHeadStandStill()
    {
        var motion = new TurbineMotion(3f);
        var end = Run(motion, 0f, 1f, 0f);
        var angle = motion.RotorAngle;

        Run(motion, end, 2f, 0f);

        Assert.Equal(0f, motion.Spin);
        Assert.Equal(angle, motion.RotorAngle);
        Assert.Equal(0f, motion.HeadYaw);
    }

    // Winding up takes a few seconds and coasting down takes longer, the way a heavy rotor behaves.
    [Fact]
    public void TheRotorWindsUpInAboutFourSecondsAndCoastsDownMoreSlowly()
    {
        var rising = new TurbineMotion(0f);
        var falling = new TurbineMotion(0f);

        rising.Step(Frame, 0f, 0f, false);
        falling.Step(Frame, 0f, 1f, false);
        Run(rising, 0f, 1f, 1f);
        Run(falling, 0f, 1f, 0f);

        var risen = rising.Spin / TurbineMotion.MaxSpin;
        var fallen = 1f - falling.Spin / TurbineMotion.MaxSpin;

        Assert.InRange(risen, 0.3f, 0.75f);
        Assert.InRange(fallen, 0.15f, 0.5f);
        Assert.True(risen > fallen + 0.1f, $"spin-up {risen} should outpace spin-down {fallen}");

        Run(rising, 1f, 4f, 1f);

        Assert.InRange(rising.Spin / TurbineMotion.MaxSpin, 1f - TurbineMotion.GustDepth, 1f + TurbineMotion.GustDepth);
    }

    [Fact]
    public void GustsMoveTheSpeedButStayWithinTheirDepth()
    {
        var motion = new TurbineMotion(0f);
        var low = float.MaxValue;
        var high = float.MinValue;
        var time = 0f;

        for (var i = 0; i < 60 * 60; i++)
        {
            time += Frame;
            motion.Step(Frame, time, 1f, false);
            low = Math.Min(low, motion.Spin);
            high = Math.Max(high, motion.Spin);
        }

        Assert.True(high - low > 0.1f * TurbineMotion.MaxSpin, $"gusts should be visible: {low} to {high}");
        Assert.InRange(low, (1f - TurbineMotion.GustDepth) * TurbineMotion.MaxSpin - 0.01f, TurbineMotion.MaxSpin);
        Assert.InRange(high, TurbineMotion.MaxSpin, (1f + TurbineMotion.GustDepth) * TurbineMotion.MaxSpin + 0.01f);
    }

    [Fact]
    public void TwoTurbinesWithDifferentPhasesDoNotTurnInStep()
    {
        var one = new TurbineMotion(0f);
        var two = new TurbineMotion(250f);

        Run(one, 0f, 5f, 0.6f);
        Run(two, 0f, 5f, 0.6f);

        Assert.NotEqual(one.Spin, two.Spin);
        Assert.NotEqual(one.HeadYaw, two.HeadYaw);
    }

    [Fact]
    public void TheTailSwingsSideOnInHeavyRainOverAFewSecondsAndBackAfter()
    {
        var motion = new TurbineMotion(0f);
        var end = Run(motion, 0f, 1f, 0.75f, isFurled: false);

        Assert.Equal(0f, motion.TailAngle, 3);

        end = Run(motion, end, 1f, 0.75f, isFurled: true);

        Assert.InRange(motion.TailAngle, 5f, TurbineMotion.FurlAngle - 10f);

        end = Run(motion, end, 4f, 0.75f, isFurled: true);

        Assert.InRange(motion.TailAngle, TurbineMotion.FurlAngle - 1f, TurbineMotion.FurlAngle);

        Run(motion, end, 6f, 0.2f, isFurled: false);

        Assert.InRange(motion.TailAngle, 0f, 1f);
    }

    [Fact]
    public void TheHeadHuntsOnlyAFewDegrees()
    {
        var motion = new TurbineMotion(0f);
        var widest = 0f;
        var time = 0f;

        for (var i = 0; i < 60 * 120; i++)
        {
            time += Frame;
            motion.Step(Frame, time, 1f, false);
            widest = Math.Max(widest, Math.Abs(motion.HeadYaw));
        }

        Assert.InRange(widest, 1f, TurbineMotion.HuntDegrees);
    }

    // A load or a hitch hands over one huge frame; the rotor must not leap round by it.
    [Fact]
    public void ALongFrameDoesNotMakeTheRotorJump()
    {
        var motion = new TurbineMotion(0f);

        motion.Step(Frame, 0f, 1f, false);

        var before = motion.RotorAngle;

        motion.Step(5f, 5f, 1f, false);

        var turned = (motion.RotorAngle - before + 360f) % 360f;

        Assert.InRange(turned, 0f, (1f + TurbineMotion.GustDepth) * TurbineMotion.MaxSpin * 0.1f + 0.01f);
    }
}
