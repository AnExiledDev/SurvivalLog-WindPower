using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

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

    /// <summary>
    /// The wind hour by hour from <paramref name="gameSeconds"/> on, as the turbines will feel it if the weather holds,
    /// so a tester can check the swing without waiting for it. Hour 0 is now.
    /// </summary>
    public static IReadOnlyList<string> Forecast(float weatherShare, int percent, double gameSeconds, int hours)
    {
        var lines = new List<string>();

        for (var hour = 0; hour <= hours; hour++)
        {
            var share = WindVariation.Share(weatherShare, percent, gameSeconds + hour * 3600.0);
            var when = hour == 0 ? "now" : $"+{hour} h";

            lines.Add($"{when}: wind {WindOutput.Percent(share)}%");
        }

        return lines;
    }

    /// <summary>A list of pieces as counts with names, in the order each first appears: "3 Scrap Metal, 2 Wire"; empty is "nothing".</summary>
    public static string Pieces(IReadOnlyList<int> pieces, Func<int, string> name) =>
        pieces.Count == 0
            ? "nothing"
            : string.Join(", ", pieces.GroupBy(id => id).Select(group => $"{group.Count()} {name(group.Key)}"));

    /// <summary>What crafting a turbine takes and what taking it down gives back now, and which setting decides the second.</summary>
    public static string Recipe(TurbineChoice choice, TurbineSettings settings, Func<int, string> name)
    {
        var from = string.IsNullOrWhiteSpace(settings.Returns) || choice.ReturnsProblem != null
            ? $"ReturnPercent {choice.Percent}%"
            : "its Returns list";

        return $"{choice.Kind.Name}: takes {Pieces(choice.Materials, name)}; taking it down gives back {Pieces(choice.Back, name)} ({from}).";
    }
}
