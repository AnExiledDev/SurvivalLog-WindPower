using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace WindPower.Core;

/// <summary>
/// How much power the Large Wind Turbine makes. The game gives every weather a wind value (0 for frost and storms, 0.1
/// on sunny days, up to 0.6 in the worst cold snaps). The turbine turns day and night, so even calm air gives a quarter
/// of its rated power, and each 0.1 of wind adds a quarter more.
/// </summary>
public static class WindOutput
{
    /// <summary>The same rating as the Large Solar Panel, which makes 24 at noon in full sun.</summary>
    public const float Rated = 24f;

    private const float Calm = 0.25f;
    private const float PerWind = 2.5f;

    public static float Power(float wind) => Rated * (Calm + PerWind * Math.Max(0f, wind));

    /// <summary>The whole number the game's power panel shows and adds up, rounded the way the game rounds.</summary>
    public static int Shown(float power) => (int)Math.Round(power);

    public static int Percent(float power) => (int)Math.Round(power / Rated * 100f);

    /// <summary>
    /// The line under the turbine's output in the power hover, built like the solar panel's: the game's own
    /// "Rated {0}" text, then the weather and how hard the wind blows as a share of the rating.
    /// </summary>
    public static string Note(string ratedFormat, string windFormat, string weatherName, float power) =>
        string.Format(CultureInfo.InvariantCulture, ratedFormat, Shown(Rated)) + ", " + string.Format(CultureInfo.InvariantCulture, windFormat, weatherName, Percent(power) + "%");

    /// <summary>
    /// Rewrites the turbines' rows in the power panel's facility list. The game builds each row the way it builds a
    /// solar panel's, so a turbine shows idle at night and 0 power; each row named in <paramref name="powerById"/> gets
    /// status "gen" and its wind power instead, keeping its name and any other field. Returns the new list and how
    /// much the panel's total must change.
    /// </summary>
    public static (string Json, int Added) Rewrite(string json, IReadOnlyDictionary<string, int> powerById)
    {
        var added = 0;

        foreach (var pair in powerById)
        {
            var prefix = "{\"id\":\"" + pair.Key + "\",";
            var from = 0;

            while (true)
            {
                var start = json.IndexOf(prefix, from, StringComparison.Ordinal);

                if (start < 0) break;

                var end = ObjectEnd(json, start);

                if (end < 0) break;

                var fields = Fields(json, start, end);
                var raw = fields.Find(field => field.Key == "power").Value;
                var old = raw != null && double.TryParse(raw,NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? (int)value : 0;
                var row = new StringBuilder("{");

                foreach (var field in fields)
                {
                    if (field.Key is "status" or "reason" or "power") continue;

                    row.Append('"').Append(field.Key).Append("\":").Append(field.Value).Append(',');

                    if (field.Key == "name") row.Append("\"status\":\"gen\",");
                }

                row.Append("\"power\":").Append(pair.Value.ToString(CultureInfo.InvariantCulture)).Append('}');

                var text = row.ToString();

                json = json.Substring(0, start) + text + json.Substring(end + 1);
                added += pair.Value - old;
                from = start + text.Length;
            }
        }

        return (json, added);
    }

    /// <summary>The index of the brace that closes the object opening at <paramref name="start"/>, or -1.</summary>
    private static int ObjectEnd(string json, int start)
    {
        var depth = 0;
        var inString = false;

        for (var i = start; i < json.Length; i++)
        {
            var c = json[i];

            if (inString)
            {
                if (c == '\\') i++;
                else if (c == '"') inString = false;

                continue;
            }

            if (c == '"') inString = true;
            else if (c == '{' || c == '[') depth++;
            else if (c == '}' || c == ']')
            {
                depth--;

                if (depth == 0) return i;
            }
        }

        return -1;
    }

    /// <summary>The object's top-level fields in order, each value as the raw JSON text it was written with.</summary>
    private static List<KeyValuePair<string, string>> Fields(string json, int start, int end)
    {
        var fields = new List<KeyValuePair<string, string>>();
        var i = start + 1;

        while (i < end)
        {
            var keyStart = json.IndexOf('"', i);

            if (keyStart < 0 || keyStart >= end) break;

            var keyEnd = StringEnd(json, keyStart);
            var key = json.Substring(keyStart + 1, keyEnd - keyStart - 1);
            var colon = json.IndexOf(':', keyEnd);
            var valueStart = colon + 1;
            var valueEnd = ValueEnd(json, valueStart, end);

            fields.Add(new KeyValuePair<string, string>(key, json.Substring(valueStart, valueEnd - valueStart).Trim()));
            i = valueEnd + 1;
        }

        return fields;
    }

    private static int StringEnd(string json, int quote)
    {
        for (var i = quote + 1; i < json.Length; i++)
        {
            if (json[i] == '\\') i++;
            else if (json[i] == '"') return i;
        }

        return json.Length - 1;
    }

    /// <summary>Where the value starting at <paramref name="at"/> stops: the comma after it, or the object's closing brace.</summary>
    private static int ValueEnd(string json, int at, int end)
    {
        var depth = 0;

        for (var i = at; i < end; i++)
        {
            var c = json[i];

            if (c == '"') i = StringEnd(json, i);
            else if (c == '{' || c == '[') depth++;
            else if (c == '}' || c == ']') depth--;
            else if (c == ',' && depth == 0) return i;
        }

        return end;
    }
}
