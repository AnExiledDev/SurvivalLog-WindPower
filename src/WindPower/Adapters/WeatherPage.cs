using System;
using System.IO;
using ExiledToolkit.Kit;
using ExiledToolkit.Web;
using WindPower.Core;

namespace WindPower.Adapters;

/// <summary>
/// Puts the wind in the HUD's weather tooltip: keeps windpower.js in the CoreUI1 page and hands it the weather's wind.
/// Nothing in the game's state is written. All members run on Unity's main thread; the web view's callback only stores
/// the answer, and the next frame hands it to <see cref="PageWatch"/>.
/// </summary>
internal sealed class WeatherPage
{
    private const string ScriptResource = "WindPower.windpower.js";

    private readonly PageWatch _watch = new();
    private readonly ReplySlot _replies = new();
    private readonly Action<string> _log;
    private string? _script;

    public WeatherPage(Action<string> log)
    {
        _log = log;
    }

    public void Tick(double now)
    {
        var turn = _watch.Step(now, _replies.Take());

        if (turn.LogLine != null) _log(turn.LogLine);

        if (turn.Send == PageSend.Nothing) return;

        var percent = WindPatches.WindPercent();
        var reading = PageCalls.Reading(percent, WindPatches.Local(TurbineRows.WindLabelKey, "Wind"), WindPatches.StrengthWord(percent));
        var javaScript = turn.Send == PageSend.Install ? PageCalls.Install(Script(), reading) : PageCalls.Check(reading);

        if (Run(javaScript, now) is { } line) _log(line);
    }

    /// <summary>Sends the call; returns a line for the log when it could not leave.</summary>
    private string? Run(string javaScript, double now)
    {
        if (WebUi.TryRun(javaScript, _replies, out var failure)) return null;

        return failure == null ? _watch.NotSent(now) : _watch.RunFailed(now, failure.Message);
    }

    /// <summary>Read on first need: most launches send it once, and some never reach a page that wants it.</summary>
    private string Script()
    {
        if (_script != null) return _script;

        using var stream = typeof(WeatherPage).Assembly.GetManifestResourceStream(ScriptResource) ?? throw new InvalidOperationException(ScriptResource + " is not embedded");
        using var reader = new StreamReader(stream);

        return _script = reader.ReadToEnd();
    }
}
