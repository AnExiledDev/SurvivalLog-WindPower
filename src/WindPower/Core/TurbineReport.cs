using System.Globalization;

namespace WindPower.Core;

/// <summary>The lines the Show turbines test button prints, kept apart from the game so a test can read them.</summary>
public static class TurbineReport
{
    /// <summary>The weather line: its name, id and the wind's share, or a note that there is no weather.</summary>
    public static string Weather(int weatherId, string weatherName, float share) =>
        weatherId < 0
            ? "no weather yet: load a save first"
            : $"weather {weatherName} ({weatherId}), wind {WindOutput.Percent(share)}%";

    /// <summary>One turbine: what it makes now in the yard and on the terrace, whether its recipe is shown, and its ids.</summary>
    public static string Turbine(TurbineKind kind, TurbineSettings settings, float share, bool? recipeShown)
    {
        var yard = WindOutput.Shown(WindOutput.Power(settings.RatedPower, share, false));
        var terrace = WindOutput.Shown(WindOutput.Power(settings.RatedPower, share, true));
        var recipe = recipeShown switch { true => "shown", false => "not shown yet", null => "unknown" };
        var rated = settings.RatedPower.ToString("0.##", CultureInfo.InvariantCulture);

        return $"{kind.Name}: rated {rated}, now {yard} in the yard and {terrace} on the 2F terrace. Recipe {kind.Ids.Recipe} {recipe}. Package item {kind.Ids.Item}. Shop price {settings.ShopPrice}, stock {settings.ShopStock}.";
    }
}
