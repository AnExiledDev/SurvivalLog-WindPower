using WindPower.Core;
using Xunit;

namespace WindPower.Tests;

public class WindOutputTests
{
    // Weather ids from Config_Weather. The table is the mod's own: the game's WindValue is read by nothing.
    [Theory]
    [InlineData(12, 0)]   // Heavy Smog: still air
    [InlineData(1, 6)]    // Clear
    [InlineData(8, 5)]    // frost-clear
    [InlineData(2, 10)]   // Cloudy
    [InlineData(3, 17)]   // Rainy
    [InlineData(11, 22)]  // Continuous Rain
    [InlineData(10, 36)]  // Heavy Rain, tail furled
    [InlineData(4, 26)]   // Mild Cold
    [InlineData(16, 38)]  // Moderate Cold Rain
    [InlineData(6, 48)]   // Severe Cold: the full rating
    public void EachWeatherGivesItsShareOfTheRatingInTheYard(int weatherId, int power)
    {
        Assert.Equal(power, WindOutput.Shown(WindOutput.Power(WindOutput.Share(weatherId), isOnTerrace: false)));
    }

    // Severe Cold on the terrace is 48 x 1.2, more than the rating: the terrace is the better spot, not a cap.
    [Theory]
    [InlineData(6, 58)]
    [InlineData(3, 20)]
    [InlineData(12, 0)]
    public void TheTerraceGivesAFifthMoreThanTheYard(int weatherId, int power)
    {
        Assert.Equal(power, WindOutput.Shown(WindOutput.Power(WindOutput.Share(weatherId), isOnTerrace: true)));
    }

    [Fact]
    public void AWeatherAGameUpdateAddsGetsACloudyDaysWind()
    {
        Assert.Equal(WindOutput.Share(2), WindOutput.Share(99));
    }

    [Fact]
    public void ABadShareCountsAsStill()
    {
        Assert.Equal(0f, WindOutput.Power(-1f, isOnTerrace: true));
        Assert.Equal(0, WindOutput.Percent(-1f));
    }

    [Fact]
    public void TheNoteShowsTheRatingTheWeatherAndTheWindsShare()
    {
        Assert.Equal("Rated 48, Rainy wind 35%", WindOutput.Note("Rated {0}", "{0} wind {1}", "2F terrace +20%", "Rainy", WindOutput.Share(3), isOnTerrace: false));
    }

    [Fact]
    public void ATerraceTurbinesNoteAddsTheTerracesBonus()
    {
        Assert.Equal("Rated 48, Rainy wind 35%, 2F terrace +20%", WindOutput.Note("Rated {0}", "{0} wind {1}", "2F terrace +20%", "Rainy", WindOutput.Share(3), isOnTerrace: true));
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
