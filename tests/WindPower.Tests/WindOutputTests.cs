using WindPower.Core;
using Xunit;

namespace WindPower.Tests;

public class WindOutputTests
{
    // A round rating so each weather's share reads easily; the turbines' own ratings are settings.
    private const float Rated = 48f;

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
        Assert.Equal(power, WindOutput.Shown(WindOutput.Power(Rated, WindOutput.Share(weatherId), isOnTerrace: false)));
    }

    // Severe Cold on the terrace is 48 x 1.2, more than the rating: the terrace is the better spot, not a cap.
    [Theory]
    [InlineData(6, 58)]
    [InlineData(3, 20)]
    [InlineData(12, 0)]
    public void TheTerraceGivesAFifthMoreThanTheYard(int weatherId, int power)
    {
        Assert.Equal(power, WindOutput.Shown(WindOutput.Power(Rated, WindOutput.Share(weatherId), isOnTerrace: true)));
    }

    [Fact]
    public void AWeatherAGameUpdateAddsGetsACloudyDaysWind()
    {
        Assert.Equal(WindOutput.Share(2), WindOutput.Share(99));
    }

    [Fact]
    public void ABadShareCountsAsStill()
    {
        Assert.Equal(0f, WindOutput.Power(Rated, -1f, isOnTerrace: true));
        Assert.Equal(0, WindOutput.Percent(-1f));
    }

    // The medium turbine is rated at half the large one, so in any weather it makes half as much.
    [Fact]
    public void OutputScalesWithTheTurbinesRating()
    {
        var share = WindOutput.Share(3);

        Assert.Equal(WindOutput.Power(36f, share, isOnTerrace: false) / 2f, WindOutput.Power(18f, share, isOnTerrace: false), 4);
        Assert.Equal(0f, WindOutput.Power(-5f, share, isOnTerrace: false));
    }

    [Fact]
    public void TheNoteShowsThatTurbinesOwnRating()
    {
        Assert.StartsWith("Rated 18,", WindOutput.Note("Rated {0}", "{0} wind {1}", "2F terrace +20%", "Rainy", 18f, WindOutput.Share(3), "Moderate", isOnTerrace: false));
    }

    [Fact]
    public void TheNoteShowsTheRatingTheWeatherAndTheWindsShare()
    {
        Assert.Equal("Rated 48, Rainy wind Moderate (35%/100%)", WindOutput.Note("Rated {0}", "{0} wind {1}", "2F terrace +20%", "Rainy", Rated, WindOutput.Share(3), "Moderate", isOnTerrace: false));
    }

    [Fact]
    public void ATerraceTurbinesNoteAddsTheTerracesBonus()
    {
        Assert.Equal("Rated 48, Rainy wind Moderate (35%/100%), 2F terrace +20%", WindOutput.Note("Rated {0}", "{0} wind {1}", "2F terrace +20%", "Rainy", Rated, WindOutput.Share(3), "Moderate", isOnTerrace: true));
    }

    // A player read a bare "30" as a wind speed and asked whether 30 was the most. The word says how strong the wind
    // is, and the "/100%" says 100% is full wind, so a gust past it reads as one.
    [Theory]
    [InlineData(12, "Calm")]       // Heavy Smog makes nothing
    [InlineData(1, "Light")]       // Clear
    [InlineData(2, "Light")]       // Cloudy
    [InlineData(3, "Moderate")]    // Rainy
    [InlineData(11, "Moderate")]   // Continuous Rain
    [InlineData(4, "Strong")]      // Mild Cold
    [InlineData(10, "Strong")]     // Heavy Rain
    [InlineData(5, "Strong")]      // Moderate Cold
    [InlineData(6, "Gale")]        // Severe Cold: the full rating
    public void EachWeathersAverageWindHasItsWord(int weatherId, string word)
    {
        Assert.Equal(word, WindOutput.Strength(WindOutput.Percent(WindOutput.Share(weatherId))).En);
    }

    [Theory]
    [InlineData(0, "Calm")]
    [InlineData(1, "Light")]
    [InlineData(29, "Light")]
    [InlineData(30, "Moderate")]
    [InlineData(49, "Moderate")]
    [InlineData(50, "Strong")]
    [InlineData(89, "Strong")]
    [InlineData(90, "Gale")]
    [InlineData(130, "Gale")]
    public void TheWordChangesAtEachBandsStart(int percent, string word)
    {
        Assert.Equal(word, WindOutput.Strength(percent).En);
    }

    [Fact]
    public void TheReadingGivesTheWordAndThePercentOutOfFullWind()
    {
        Assert.Equal("Light (30%/100%)", WindOutput.Reading("Light", 30));
        Assert.Equal("Gale (130%/100%)", WindOutput.Reading("Gale", 130));
    }

    [Fact]
    public void EveryStrengthWordHasChinese()
    {
        Assert.All(WindOutput.Strengths, strength => Assert.False(string.IsNullOrWhiteSpace(strength.Word.Zh)));
        Assert.All(WindOutput.Strengths, strength => Assert.Contains(strength.Word, TurbineRows.SharedTexts));
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
