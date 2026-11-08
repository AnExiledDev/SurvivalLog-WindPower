using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace WindPower.Core;

/// <summary>
/// How much power a wind turbine makes. It turns day and night at a share of its rating that the weather sets (see
/// <see cref="WeatherWinds"/>, each weather's wind a setting). The game's own Config_Weather.WindValue is read by
/// nothing in the game, so the mod keeps its own table. Each turbine's rating is a setting, set below its solar
/// panel's since the wind never stops at night.
/// </summary>
public static class WindOutput
{
    /// <summary>The 2F terrace stands higher and more open than the yard.</summary>
    public const float TerraceBonus = 1.2f;

    /// <summary>The share of the rating each weather gives at the default settings, by Config_Weather id.</summary>
    public static float Share(int weatherId) => Share(weatherId, WeatherWinds.Defaults);

    /// <summary>
    /// The share of the rating the weather gives with these settings (percents in the order of
    /// <see cref="WeatherWinds.All"/>). A missing or negative setting counts as still air.
    /// </summary>
    public static float Share(int weatherId, IReadOnlyList<int> percents)
    {
        var index = WeatherWinds.IndexOfWeather(weatherId);

        return index < percents.Count ? Math.Clamp(percents[index], 0, WeatherWinds.MostPercent) / 100f : 0f;
    }

    /// <summary>The weather's share at the default settings, with Heavy Smog given <paramref name="calm"/> of a clear day's wind.</summary>
    public static float Share(int weatherId, float calm) => Share(weatherId, WeatherWinds.Defaults, calm);

    /// <summary>
    /// The weather's share with these settings, and Heavy Smog, while its own setting is 0, given <paramref name="calm"/>
    /// (0 to 1) of a clear day's wind (another mod's smog setting, see <see cref="WindBoosts.AddCalm"/>). Every other
    /// weather keeps its own, 0 included.
    /// </summary>
    public static float Share(int weatherId, IReadOnlyList<int> percents, float calm)
    {
        var share = Share(weatherId, percents);

        if (share > 0f || WeatherWinds.IndexOfWeather(weatherId) != WeatherWinds.SmogIndex) return share;

        var clear = WeatherWinds.ClearIndex < percents.Count ? Math.Clamp(percents[WeatherWinds.ClearIndex], 0, WeatherWinds.MostPercent) / 100f : 0f;

        return Math.Clamp(calm, 0f, 1f) * clear;
    }

    public static float Power(float rated, float share, bool isOnTerrace) => Math.Max(0f, rated) * Math.Max(0f, share) * (isOnTerrace ? TerraceBonus : 1f);

    /// <summary>The whole number the game's power panel shows and adds up, rounded the way the game rounds.</summary>
    public static int Shown(float power) => (int)Math.Round(power);

    public static int Percent(float share) => (int)Math.Round(Math.Max(0f, share) * 100f);

    /// <summary>
    /// The word for how hard the wind blows, by its percent of the rating. Smog is still air; clear and cloudy days
    /// stay light; rain is moderate; the cold snaps and Heavy Rain are strong, and Severe Cold is a gale.
    /// </summary>
    public static readonly IReadOnlyList<(int From, Text Word)> Strengths = new[]
    {
        (0, new Text("WindPower_WindCalm", "Calm", "无风")),
        (1, new Text("WindPower_WindLight", "Light", "微风")),
        (30, new Text("WindPower_WindModerate", "Moderate", "和风")),
        (50, new Text("WindPower_WindStrong", "Strong", "强风")),
        (90, new Text("WindPower_WindGale", "Gale", "大风")),
    };

    /// <summary>The strongest word whose start the percent has reached.</summary>
    public static Text Strength(int percent)
    {
        var word = Strengths[0].Word;

        foreach (var (from, text) in Strengths)
            if (percent >= from) word = text;

        return word;
    }

    /// <summary>The wind as players read it: the word, then its percent of the rating out of the 100% of full wind.</summary>
    public static string Reading(string word, int percent) =>
        word + " (" + Math.Max(0, percent).ToString(CultureInfo.InvariantCulture) + "%/100%)";

    /// <summary>
    /// The line under the turbine's output in the power hover, built like the solar panel's: the game's own
    /// "Rated {0}" text, the weather and how hard the wind blows (its word and its share of the rating), then the terrace's bonus
    /// when the turbine stands there.
    /// </summary>
    public static string Note(string ratedFormat, string windFormat, string terraceText, string weatherName, float rated, float share, string strength, bool isOnTerrace)
    {
        var note = string.Format(CultureInfo.InvariantCulture, ratedFormat, Shown(rated)) + ", " + string.Format(CultureInfo.InvariantCulture, windFormat, weatherName, Reading(strength, Percent(share)));

        return isOnTerrace ? note + ", " + terraceText : note;
    }

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
