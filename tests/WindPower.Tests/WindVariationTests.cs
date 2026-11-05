using System;
using System.Linq;
using WindPower.Core;
using Xunit;

namespace WindPower.Tests;

public class WindVariationTests
{
    private const int Hour = 3600;
    private const int Tick = 600; // the game settles power every 10 game minutes
    private const float Rainy = 0.35f;

    private static float[] Ticks(int days, int percent, float share = Rainy) => Enumerable.Range(0, days * 144).Select(tick => WindVariation.Share(share, percent, (double)tick * Tick)).ToArray();

    // The weather sets the average; the swing must not quietly make turbines better or worse over a game.
    [Fact]
    public void OverAMonthTheWindAveragesTheWeathersOwn()
    {
        var shares = Ticks(30, WindVariation.MostPercent);

        Assert.InRange(shares.Average(), Rainy * 0.95f, Rainy * 1.05f);
    }

    [Fact]
    public void TheWindRisesAndFallsWithinOneWeather()
    {
        var shares = Ticks(2, WindVariation.DefaultPercent);

        Assert.True(shares.Max() > Rainy * 1.15f, $"strongest {shares.Max()}");
        Assert.True(shares.Min() < Rainy * 0.85f, $"calmest {shares.Min()}");
    }

    [Fact]
    public void TheSwingNeverPassesTheSetting()
    {
        var shares = Ticks(60, 30);

        Assert.InRange(shares.Min(), Rainy * 0.7f - 1e-5f, Rainy * 1.3f + 1e-5f);
        Assert.InRange(shares.Max(), Rainy * 0.7f - 1e-5f, Rainy * 1.3f + 1e-5f);
    }

    // One power tick to the next is a gentle change, not a jump: the wind swells over hours.
    [Fact]
    public void TheWindChangesSmoothlyFromOneTickToTheNext()
    {
        var shares = Ticks(30, WindVariation.MostPercent, share: 1f);
        var biggestStep = shares.Zip(shares.Skip(1), (a, b) => Math.Abs(b - a)).Max();

        Assert.True(biggestStep < 0.08f, $"biggest step {biggestStep}");
    }

    [Fact]
    public void ZeroKeepsTheWindSteady()
    {
        Assert.All(Ticks(3, 0), share => Assert.Equal(Rainy, share));
    }

    [Fact]
    public void StillAirStaysStill()
    {
        Assert.All(Ticks(3, WindVariation.MostPercent, share: 0f), share => Assert.Equal(0f, share));
    }

    // The swing comes from the clock alone, so every turbine sees the same wind and a loaded save resumes it.
    [Fact]
    public void TheSameMomentAlwaysHasTheSameWind()
    {
        var moment = 37.0 * 86400 + 5 * Hour + 1234;

        Assert.Equal(WindVariation.Share(Rainy, 30, moment), WindVariation.Share(Rainy, 30, moment));
        Assert.NotEqual(WindVariation.Share(Rainy, 30, moment), WindVariation.Share(Rainy, 30, moment + 3 * Hour));
    }

    [Fact]
    public void ASettingPastTheLimitIsHeldToIt()
    {
        var moment = 12.0 * Hour;

        Assert.Equal(WindVariation.Share(Rainy, WindVariation.MostPercent, moment), WindVariation.Share(Rainy, 500, moment));
        Assert.Equal(Rainy, WindVariation.Share(Rainy, -20, moment));
    }
}
