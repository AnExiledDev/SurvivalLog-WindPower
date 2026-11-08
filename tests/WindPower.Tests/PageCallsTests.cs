using System.Text.Json;
using WindPower.Core;
using Xunit;

namespace WindPower.Tests;

public class PageCallsTests
{
    /// <summary>A JSON string literal is a JavaScript one, so a text that survives a JSON read survives the page.</summary>
    [Theory]
    [InlineData("var a = 1;")]
    [InlineData("el.className = \"weather-tt-row\" + '';")]
    [InlineData("var re = /CoreUI1\\.html/i;")]
    [InlineData("two\nlines\r\tand a tab")]
    [InlineData("ends with a backslash\\")]
    [InlineData("line\u2028separator\u2029")]
    public void ThePageScriptReachesThePageExactlyAsWritten(string script)
    {
        var literal = PageCalls.Quote(script);

        Assert.Equal(script, JsonSerializer.Deserialize<string>(literal));
        Assert.DoesNotContain('\n', literal);
        Assert.DoesNotContain('\u2028', literal);
        Assert.DoesNotContain('\u2029', literal);
    }

    [Fact]
    public void TheInstallCallCarriesTheWholeScript()
    {
        const string script = "(function(){window.__slwind={apply:function(){return \"ok\";}};})();\n// last line";

        Assert.Contains(PageCalls.Quote(script), PageCalls.Install(script, PageCalls.Reading(35, "Wind", "Moderate")));
    }

    [Fact]
    public void EveryCallHandsThePageTheReading()
    {
        var reading = PageCalls.Reading(35, "Wind \"Now\"", "Moderate");

        Assert.Equal("{pct:35,label:\"Wind \\\"Now\\\"\",value:\"Moderate (35%/100%)\"}", reading);
        Assert.Contains(reading, PageCalls.Check(reading));
        Assert.Contains(reading, PageCalls.Install("", reading));
    }
}
