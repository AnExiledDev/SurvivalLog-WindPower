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
}
