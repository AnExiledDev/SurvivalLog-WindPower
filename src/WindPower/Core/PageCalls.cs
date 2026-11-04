using System.Globalization;
using System.Text;

namespace WindPower.Core;

/// <summary>
/// The JavaScript the plugin runs in the game's web view. The web view runs it in Root.html; the HUD's weather tooltip
/// lives in the CoreUI1 page, an iframe inside Root, so every call first finds that frame and then talks to
/// windpower.js inside it. Each call answers with one of the <see cref="PageAnswer"/> strings.
/// </summary>
public static class PageCalls
{
    private const string Api = "w.__slwind";

    /// <summary>The frame search the other CoreUI1 mods use: the iframe whose page is CoreUI1.html.</summary>
    private const string FindFrame =
        "var fs=document.querySelectorAll('iframe'),w=null;" +
        "for(var i=0;i<fs.length;i++){var c=fs[i].contentWindow;if(!c)continue;" +
        "try{if(!/CoreUI1\\.html/i.test(String(c.location)))continue;}catch(e){continue;}w=c;break;}" +
        "if(!w)return '" + PageAnswer.NoPage + "';";

    /// <summary>Cheap enough to send every couple of seconds: says whether the script is there, and hands it the reading again.</summary>
    public static string Check(string reading) => Wrap($"if(!{Api})return '{PageAnswer.Missing}';{Apply(reading)}");

    /// <summary>Evaluates the script inside the CoreUI1 frame, so it sees that page's tooltip and styles.</summary>
    public static string Install(string script, string reading) =>
        Wrap($"w.eval({Quote(script)});if(!{Api})return '{PageAnswer.InstallLeftNothing}';{Apply(reading)}");

    /// <summary>The reading as the page script takes it: the wind as a whole percent, or -1 with no weather, and the row's label.</summary>
    public static string Reading(int percent, string label) =>
        $"{{pct:{percent.ToString(CultureInfo.InvariantCulture)},label:{Quote(label)}}}";

    /// <summary>A JavaScript string literal that no quote, backslash or line break in the text can end early.</summary>
    public static string Quote(string text)
    {
        var literal = new StringBuilder(text.Length + 2).Append('"');

        foreach (var c in text)
        {
            switch (c)
            {
                case '"':
                    literal.Append("\\\"");
                    break;
                case '\\':
                    literal.Append("\\\\");
                    break;
                case < ' ' or '\u2028' or '\u2029':
                    literal.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    break;
                default:
                    literal.Append(c);
                    break;
            }
        }

        return literal.Append('"').ToString();
    }

    private static string Apply(string reading) => $"return String({Api}.apply({reading}));";

    private static string Wrap(string body) => "(function(){try{" + FindFrame + body + "}catch(e){return 'error: '+e;}})()";
}
