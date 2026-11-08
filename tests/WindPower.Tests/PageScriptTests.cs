using WindPower.Core;
using Xunit;

namespace WindPower.Tests;

public class PageScriptTests
{
    private const string AllRows = "Light Intensity|Cold Intensity|Wind|Tomorrow";

    private static string Reading(int percent, string label = "Wind", string word = "Moderate") => PageCalls.Reading(percent, label, word);

    // The game's tooltip reads Light, Cold, then Tomorrow; wind belongs with the intensities, above the forecast.
    [Fact]
    public void TheWindRowSitsWithTheIntensitiesAboveTomorrow()
    {
        var page = PageScript.On();

        Assert.Equal(PageAnswer.Ok, page.Apply(Reading(35)));
        Assert.Equal(AllRows, page.Text("rowLabels(page.tooltip)"));
        Assert.Equal("Moderate (35%/100%)", page.Text("windRow().children[1].textContent"));
        Assert.True(page.Is("windRow().classList.contains('weather-tt-row')"));
    }

    [Fact]
    public void WithNoForecastTheWindRowIsLast()
    {
        var page = PageScript.On("{hasNoTomorrow:true}");

        page.Apply(Reading(75));

        Assert.Equal("Light Intensity|Cold Intensity|Wind", page.Text("rowLabels(page.tooltip)"));
    }

    [Fact]
    public void AChineseGameGetsItsOwnLabel()
    {
        var page = PageScript.On();

        page.Apply(Reading(20, "风力", "微风"));

        Assert.Equal("风力", page.Text("windRow().children[0].textContent"));
        Assert.Equal("微风 (20%/100%)", page.Text("windRow().children[1].textContent"));
    }

    [Fact]
    public void WithNoWeatherTheWindRowIsTakenAway()
    {
        var page = PageScript.On();

        page.Apply(Reading(35));

        Assert.Equal(PageAnswer.Ok, page.Apply(Reading(-1)));
        Assert.False(page.Is("windRow()"));
        Assert.Equal("Light Intensity|Cold Intensity|Tomorrow", page.Text("rowLabels(page.tooltip)"));
    }

    [Fact]
    public void WithoutATooltipNothingIsDrawn()
    {
        var page = PageScript.On("{hasNoTooltip:true}");

        Assert.Equal(PageAnswer.NoTooltip, page.Apply(Reading(35)));
        Assert.False(page.Is("windRow()"));
    }

    [Fact]
    public void TheValueFollowsTheWeather()
    {
        var page = PageScript.On();

        page.Apply(Reading(35));
        page.Apply(Reading(100, word: "Gale"));

        Assert.Equal("Gale (100%/100%)", page.Text("windRow().children[1].textContent"));
        Assert.Equal(1, page.Number("document.querySelectorAll('.slwind-row').length"));
    }

    // The check comes every couple of seconds; a weather that has not changed must not redraw the hovered tooltip.
    [Fact]
    public void AnUnchangedReadingWritesNothing()
    {
        var page = PageScript.On();

        page.Apply(Reading(35));
        page.Run("var before = windRow().children[1].writes;");
        page.Apply(Reading(35));

        Assert.Equal(0, page.Number("windRow().children[1].writes - before"));
    }

    // Vue drops and adds Tomorrow as the forecast comes and goes; the next check puts the row back above it.
    [Fact]
    public void ATomorrowAddedAfterTheRowStillComesLast()
    {
        var page = PageScript.On("{hasNoTomorrow:true}");

        page.Apply(Reading(35));
        page.Run("addRow(page.tooltip, 'weather-tt-row weather-tt-next', 'Tomorrow', 'Clear');");
        page.Apply(Reading(35));

        Assert.Equal(AllRows, page.Text("rowLabels(page.tooltip)"));
    }

    // A HUD rebuilt after a load has a new tooltip; the row moves to it instead of staying with the old one.
    [Fact]
    public void ARebuiltTooltipGetsTheRow()
    {
        var page = PageScript.On();

        page.Apply(Reading(35));
        page.Run("page.hud.removeChild(page.tooltip); page.tooltip = drawTooltip(page.hud, true);");
        page.Apply(Reading(35));

        Assert.Equal(AllRows, page.Text("rowLabels(page.tooltip)"));
        Assert.Equal(1, page.Number("document.querySelectorAll('.slwind-row').length"));
    }

    // The plugin resends the script when it thinks the page lost it; a second copy must not draw a second row.
    [Fact]
    public void SendingTheScriptAgainKeepsOneRow()
    {
        var page = PageScript.On();

        page.Apply(Reading(35));
        page.Reinstall();
        page.Apply(Reading(40));

        Assert.Equal(1, page.Number("document.querySelectorAll('.slwind-row').length"));
        Assert.Equal("Moderate (40%/100%)", page.Text("windRow().children[1].textContent"));
    }
}
