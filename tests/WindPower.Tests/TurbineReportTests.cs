using System;
using System.Linq;
using WindPower.Core;
using Xunit;

namespace WindPower.Tests;

// The Show turbines test button is how a tester checks the numbers in game, so its lines must give the real ones.
public class TurbineReportTests
{
    private static readonly TurbineSettings Settings = new(40f, "20004:4", 900, 2);

    [Fact]
    public void WithNoWeatherItAsksForASave()
    {
        Assert.Equal("no weather yet: load a save first", TurbineReport.Weather(-1, "", 0f));
    }

    [Fact]
    public void TheWeatherLineGivesTheWindAsAPercent()
    {
        Assert.Equal("weather Severe Cold (6), wind 100%", TurbineReport.Weather(6, "Severe Cold", WindOutput.Share(6)));
    }

    // Full wind: 40 in the yard, 40 x 1.2 = 48 on the terrace.
    [Fact]
    public void ATurbineLineGivesYardAndTerracePowerFromItsSetting()
    {
        var line = TurbineReport.Turbine(TurbineRows.Large, Settings, 1f, recipeShown: true);

        Assert.Contains("rated 40, now 40 in the yard and 48 on the 2F terrace", line);
        Assert.Contains($"Recipe {TurbineRows.Large.Ids.Recipe} shown.", line);
        Assert.Contains($"Package item {TurbineRows.Large.Ids.Item}.", line);
        Assert.Contains("Shop price 900, stock 2.", line);
    }

    [Theory]
    [InlineData(false, "not shown yet")]
    [InlineData(null, "unknown")]
    public void ATurbineLineSaysWhenItsRecipeIsNotShown(bool? shown, string word)
    {
        Assert.Contains($"Recipe {TurbineRows.Medium.Ids.Recipe} {word}.", TurbineReport.Turbine(TurbineRows.Medium, Settings, 0.5f, shown));
    }

    // The forecast is what lets the varying wind be checked without waiting game hours, so it must be the wind the
    // turbines really get at each hour, and flat when the swing is set to 0.
    [Fact]
    public void TheForecastGivesTheWindTheTurbinesGetEachHour()
    {
        const double now = 123456;
        var lines = TurbineReport.Forecast(0.5f, 30, now, 12);

        Assert.Equal(13, lines.Count);
        Assert.Equal($"now: wind {WindOutput.Percent(WindVariation.Share(0.5f, 30, now))}%", lines[0]);
        Assert.Equal($"+5 h: wind {WindOutput.Percent(WindVariation.Share(0.5f, 30, now + 5 * 3600))}%", lines[5]);
        Assert.True(lines.Distinct().Count() > 1, "with a 30% swing the wind should change over 12 hours");
    }

    [Fact]
    public void WithNoSwingTheForecastIsSteady()
    {
        Assert.All(TurbineReport.Forecast(0.5f, 0, 99999, 12), line => Assert.EndsWith("wind 50%", line));
    }

    private static string Name(int id) => id switch
    {
        20004 => "Scrap Metal",
        20104 => "Wire",
        20210 => "Electronic Components",
        20373 => "Stainless Steel Plate",
        _ => id.ToString(),
    };

    [Fact]
    public void PiecesAreCountedByItemInTheOrderTheyFirstAppear()
    {
        Assert.Equal("2 Wire, 3 Scrap Metal", TurbineReport.Pieces(new[] { 20104, 20004, 20104, 20004, 20004 }, Name));
        Assert.Equal("nothing", TurbineReport.Pieces(Array.Empty<int>(), Name));
    }

    // The tester takes a turbine down and compares what came back with this line, so it must name the setting that
    // decided it: a blank Returns follows ReturnPercent, a filled one is given back as written.
    [Fact]
    public void TheRecipeLineSaysWhichSettingDecidesWhatComesBack()
    {
        var allBack = TurbineRows.Large.Defaults with { ReturnPercent = 100 };
        var byHand = TurbineRows.Large.Defaults with { Returns = "20373:2" };
        var allBackChoices = TurbineChoices.Of(new[] { TurbineRows.Medium.Defaults, allBack, TurbineRows.Small.Defaults }, _ => true, _ => null);
        var byHandChoices = TurbineChoices.Of(new[] { TurbineRows.Medium.Defaults, byHand, TurbineRows.Small.Defaults }, _ => true, _ => null);

        Assert.Equal(
            "Large Wind Turbine: takes 5 Scrap Metal, 3 Wire, 3 Electronic Components, 1 Stainless Steel Plate; taking it down gives back 5 Scrap Metal, 3 Wire, 3 Electronic Components, 1 Stainless Steel Plate (ReturnPercent 100%).",
            TurbineReport.Recipe(allBackChoices[1], allBack, Name));
        Assert.EndsWith("gives back 2 Stainless Steel Plate (its Returns list).", TurbineReport.Recipe(byHandChoices[1], byHand, Name));
    }
}
