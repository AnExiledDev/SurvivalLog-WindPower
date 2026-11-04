using WindPower.Core;
using Xunit;

namespace WindPower.Tests;

public class WindOutputTests
{
    // The wind values are the game's own, from Config_Weather: 0 frost and storms, 0.1 sunny, 0.2 rain, 0.6 the worst cold snap.
    [Theory]
    [InlineData(0f, 6)]
    [InlineData(0.1f, 12)]
    [InlineData(0.2f, 18)]
    [InlineData(0.6f, 42)]
    public void MoreWindMakesMorePowerAndCalmAirStillTurnsIt(float wind, int power)
    {
        Assert.Equal(power, WindOutput.Shown(WindOutput.Power(wind)));
    }

    [Fact]
    public void ABadWindValueCountsAsCalm()
    {
        Assert.Equal(WindOutput.Power(0f), WindOutput.Power(-1f));
    }

    [Fact]
    public void TheNoteShowsTheRatingTheWeatherAndTheShareOfTheRating()
    {
        Assert.Equal("Rated 24, Sunny wind 50%", WindOutput.Note("Rated {0}", "{0} wind {1}", "Sunny", WindOutput.Power(0.1f)));
    }

    private const string Panel =
        "[{\"id\":\"11\",\"name\":\"Large Solar Panel\",\"status\":\"gen\",\"power\":24}," +
        "{\"id\":\"22\",\"name\":\"Large \\\"Wind\\\" Turbine, {big}\",\"status\":\"idle\",\"reason\":\"Night\",\"power\":0}," +
        "{\"id\":\"33\",\"name\":\"Lamp\",\"status\":\"on\",\"power\":3}]";

    [Fact]
    public void ATurbineRowGeneratesItsWindPowerAndKeepsItsName()
    {
        var (json, added) = WindOutput.Rewrite(Panel, new Dictionary<string, int> { ["22"] = 12 });

        Assert.Contains("{\"id\":\"22\",\"name\":\"Large \\\"Wind\\\" Turbine, {big}\",\"status\":\"gen\",\"power\":12}", json);
        Assert.DoesNotContain("Night", json);
        Assert.Equal(12, added);
    }

    [Fact]
    public void OtherRowsAreLeftExactlyAsTheGameWroteThem()
    {
        var (json, _) = WindOutput.Rewrite(Panel, new Dictionary<string, int> { ["22"] = 12 });

        Assert.StartsWith("[{\"id\":\"11\",\"name\":\"Large Solar Panel\",\"status\":\"gen\",\"power\":24},", json);
        Assert.EndsWith(",{\"id\":\"33\",\"name\":\"Lamp\",\"status\":\"on\",\"power\":3}]", json);
    }

    // The game already counted whatever power it gave the row, so only the difference is added to the panel's total.
    [Fact]
    public void TheTotalGrowsOnlyByWhatTheGameDidNotAlreadyCount()
    {
        var gave = Panel.Replace("\"power\":0}", "\"power\":5}");

        Assert.Equal(7, WindOutput.Rewrite(gave, new Dictionary<string, int> { ["22"] = 12 }).Added);
    }

    [Fact]
    public void AnIdThatIsOnlyThePrefixOfAnotherIsNotRewritten()
    {
        var (json, added) = WindOutput.Rewrite(Panel, new Dictionary<string, int> { ["2"] = 12, ["3"] = 9 });

        Assert.Equal(Panel, json);
        Assert.Equal(0, added);
    }
}
