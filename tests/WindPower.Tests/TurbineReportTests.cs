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
}
