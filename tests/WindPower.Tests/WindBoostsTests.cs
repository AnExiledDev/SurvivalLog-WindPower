using WindPower.Core;
using System;
using System.Collections.Generic;
using Xunit;

namespace WindPower.Tests;

// Other mods (Power Manager's wind setting, New Loop Plus's wind talents) reach the turbines only through these
// multipliers, so each test removes what it added: the list is shared by the whole game.
public class WindBoostsTests
{
    [Fact]
    public void WithNoMultiplierTheTurbinesMakeTheirOwnRating()
    {
        Assert.Equal(1f, WindBoosts.Factor());
    }

    [Fact]
    public void TwoModsMultipliersStackByMultiplying()
    {
        WindBoosts.Add("Power Manager", () => 1.5f);
        WindBoosts.Add("New Loop Plus", () => 1.2f);

        try
        {
            Assert.Equal(1.8f, WindBoosts.Factor(), 4);
        }
        finally
        {
            WindBoosts.Remove("Power Manager");
            WindBoosts.Remove("New Loop Plus");
        }
    }

    // A mod that adds or removes a multiplier from inside its own one still leaves the turbines working.
    [Fact]
    public void AMultiplierThatAddsAnotherDoesNotBreakTheRead()
    {
        WindBoosts.Add("Power Manager", () =>
        {
            WindBoosts.Add("Late Mod", () => 3f);

            return 2f;
        });

        try
        {
            Assert.Equal(2f, WindBoosts.Factor(), 4);
            Assert.Equal(6f, WindBoosts.Factor(), 4);
        }
        finally
        {
            WindBoosts.Remove("Power Manager");
            WindBoosts.Remove("Late Mod");
        }
    }

    // A setting changed in game applies at the next read, without the mod adding itself again.
    [Fact]
    public void ALiveSettingIsReadAgainEachTime()
    {
        var setting = 2f;
        WindBoosts.Add("Power Manager", () => setting);

        try
        {
            Assert.Equal(2f, WindBoosts.Factor());
            setting = 0.5f;
            Assert.Equal(0.5f, WindBoosts.Factor());
        }
        finally
        {
            WindBoosts.Remove("Power Manager");
        }
    }

    [Fact]
    public void AddingAgainUnderTheSameNameReplacesTheOldMultiplier()
    {
        WindBoosts.Add("Power Manager", () => 3f);
        WindBoosts.Add("Power Manager", () => 2f);

        try
        {
            Assert.Equal(2f, WindBoosts.Factor());
        }
        finally
        {
            WindBoosts.Remove("Power Manager");
        }

        Assert.Equal(1f, WindBoosts.Factor());
    }

    [Fact]
    public void ANegativeMultiplierStopsTheTurbinesRatherThanRunningThemBackwards()
    {
        WindBoosts.Add("Odd mod", () => -2f);

        try
        {
            Assert.Equal(0f, WindBoosts.Factor());
        }
        finally
        {
            WindBoosts.Remove("Odd mod");
        }
    }

    [Fact]
    public void AMultiplierThatIsNotANumberIsSkipped()
    {
        WindBoosts.Add("Odd mod", () => float.NaN);
        WindBoosts.Add("Power Manager", () => 1.5f);

        try
        {
            Assert.Equal(1.5f, WindBoosts.Factor());
        }
        finally
        {
            WindBoosts.Remove("Odd mod");
            WindBoosts.Remove("Power Manager");
        }
    }

    // A broken mod must not take the turbines down with it, or keep logging every second.
    [Fact]
    public void AMultiplierThatThrowsIsDroppedAndReportedOnce()
    {
        var reported = new List<string>();
        WindBoosts.OnFailed = reported.Add;
        WindBoosts.Add("Broken mod", () => throw new InvalidOperationException());
        WindBoosts.Add("Power Manager", () => 2f);

        try
        {
            Assert.Equal(2f, WindBoosts.Factor());
            Assert.Equal(2f, WindBoosts.Factor());
            Assert.Equal(new[] { "Broken mod" }, reported);
        }
        finally
        {
            WindBoosts.Remove("Power Manager");
            WindBoosts.OnFailed = _ => { };
        }
    }

    // Power Manager's calm setting is the only thing that stirs Heavy Smog, so with none the turbines stand still there.
    [Fact]
    public void WithNoCalmSettingCalmAirStaysStill()
    {
        Assert.Equal(0f, WindBoosts.CalmShare());
        Assert.Equal(0f, WindOutput.Share(12, WindBoosts.CalmShare()));
    }

    [Fact]
    public void ACalmSettingGivesHeavySmogThatShareOfAClearDay()
    {
        var setting = 0.5f;
        WindBoosts.AddCalm("Power Manager", () => setting);

        try
        {
            Assert.Equal(0.5f, WindBoosts.CalmShare());
            Assert.Equal(0.06f, WindOutput.Share(12, WindBoosts.CalmShare()), 4);
            setting = 1f;
            Assert.Equal(WindOutput.Share(1), WindOutput.Share(12, WindBoosts.CalmShare()), 4);
        }
        finally
        {
            WindBoosts.RemoveCalm("Power Manager");
        }
    }

    // Two mods giving calm air wind do not add up past a clear day; the stronger one counts.
    [Fact]
    public void TheStrongestCalmSettingCountsAndNoneGoesPastAClearDay()
    {
        WindBoosts.AddCalm("Power Manager", () => 0.3f);
        WindBoosts.AddCalm("Other mod", () => 0.6f);

        try
        {
            Assert.Equal(0.6f, WindBoosts.CalmShare());
            WindBoosts.AddCalm("Other mod", () => 5f);
            Assert.Equal(1f, WindBoosts.CalmShare());
        }
        finally
        {
            WindBoosts.RemoveCalm("Power Manager");
            WindBoosts.RemoveCalm("Other mod");
        }
    }

    // The calm setting is not a multiplier: it must not make the turbines stronger in any other weather.
    [Fact]
    public void ACalmSettingLeavesOtherWeathersAndTheMultipliersAlone()
    {
        WindBoosts.AddCalm("Power Manager", () => 1f);

        try
        {
            Assert.Equal(1f, WindBoosts.Factor());
            Assert.Equal(WindOutput.Share(3), WindOutput.Share(3, WindBoosts.CalmShare()));
        }
        finally
        {
            WindBoosts.RemoveCalm("Power Manager");
        }
    }

    [Fact]
    public void ACalmSettingThatThrowsIsDroppedAndReportedOnce()
    {
        var reported = new List<string>();
        WindBoosts.OnFailed = reported.Add;
        WindBoosts.AddCalm("Broken mod", () => throw new InvalidOperationException());

        try
        {
            Assert.Equal(0f, WindBoosts.CalmShare());
            Assert.Equal(0f, WindBoosts.CalmShare());
            Assert.Equal(new[] { "Broken mod" }, reported);
        }
        finally
        {
            WindBoosts.OnFailed = _ => { };
        }
    }
}
