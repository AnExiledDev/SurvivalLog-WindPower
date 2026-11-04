using System;
using System.Collections.Generic;

namespace WindPower.Core;

public enum PageSend
{
    Nothing,
    Check,
    Install
}

/// <summary>What to send to the page this frame, and a line for the log if something worth reporting happened.</summary>
public readonly record struct PageTurn(PageSend Send, string? LogLine);

/// <summary>
/// Keeps windpower.js alive in the CoreUI1 page. The web view can reload the page at any time and the script goes with it,
/// so the page is checked every couple of seconds and the script is sent again when the page says it is missing.
/// One call is in flight at a time. Each distinct answer is logged once, so a page that sits on the main menu all
/// session costs one line.
/// </summary>
public sealed class PageWatch
{
    public const double CheckSeconds = 2;
    public const double AnswerWaitSeconds = 5;

    /// <summary>A script that fails to install fails the same way every time; trying every two seconds would only fill the log.</summary>
    public const double InstallRetrySeconds = 30;

    public const int MostInstallsLogged = 10;
    public const int MostAnswersLogged = 30;

    private readonly HashSet<string> _loggedAnswers = new();
    private PageSend _awaiting = PageSend.Nothing;
    private double _answerDueAt;
    private double _nextCheckAt;
    private double _nextInstallAt;
    private bool _isInstallWanted;
    private int _installs;

    /// <summary>One step per frame. <paramref name="reply"/> is what the page answered since the last step, or null.</summary>
    public PageTurn Step(double now, string? reply)
    {
        if (_awaiting != PageSend.Nothing)
        {
            if (reply == null && now < _answerDueAt) return new PageTurn(PageSend.Nothing, null);

            return new PageTurn(PageSend.Nothing, Settle(reply ?? PageAnswer.NoAnswer, now));
        }

        if (_isInstallWanted && now >= _nextInstallAt) return Send(PageSend.Install, now);

        if (now >= _nextCheckAt) return Send(PageSend.Check, now);

        return new PageTurn(PageSend.Nothing, null);
    }

    /// <summary>The call from the last step never left: the game's web view is not up yet.</summary>
    public string? NotSent(double now) => Settle(PageAnswer.NoWebView, now);

    /// <summary>The call from the last step never left: the web view threw. Each different message is logged once.</summary>
    public string? RunFailed(double now, string message) => Settle(PageAnswer.RunFailedPrefix + message, now);

    private PageTurn Send(PageSend send, double now)
    {
        _awaiting = send;
        _answerDueAt = now + AnswerWaitSeconds;

        return new PageTurn(send, null);
    }

    private string? Settle(string answer, double now)
    {
        var sent = _awaiting;
        var isUnknown = answer is PageAnswer.NoWebView or PageAnswer.NoAnswer || answer.StartsWith(PageAnswer.RunFailedPrefix, StringComparison.Ordinal);
        _awaiting = PageSend.Nothing;
        _nextCheckAt = now + CheckSeconds;

        // An install answered "nopage" lost a race with a scene change: the frame went away between the check and the
        // install. That says nothing about the script, so it is settled like a check and the next check decides again.
        if (sent == PageSend.Install && !isUnknown && answer != PageAnswer.NoPage) return SettleInstall(answer, now);

        if (isUnknown && sent == PageSend.Install) _nextInstallAt = now + CheckSeconds;
        else if (!isUnknown) _isInstallWanted = answer == PageAnswer.Missing;

        return LogOnce(answer, Describe(answer));
    }

    /// <summary>An answer the script gave means it is in the page, even when it found the page unfit and changed nothing.</summary>
    private string? SettleInstall(string answer, double now)
    {
        var isInstalled = answer is PageAnswer.Ok or PageAnswer.NoTooltip;
        _installs++;
        _isInstallWanted = !isInstalled;

        if (!isInstalled)
        {
            _nextInstallAt = now + InstallRetrySeconds;

            return LogOnce("install: " + answer, $"sending the page script failed, trying again every {InstallRetrySeconds:0}s: {Describe(answer)}");
        }

        _loggedAnswers.Add(answer);

        return _installs <= MostInstallsLogged ? $"page script sent to CoreUI1 (install {_installs}): {Describe(answer)}" : null;
    }

    private string? LogOnce(string key, string line) =>
        _loggedAnswers.Count < MostAnswersLogged && _loggedAnswers.Add(key) ? line : null;

    private static string Describe(string answer) => answer switch
    {
        PageAnswer.Ok => "the wind row is in the weather tooltip",
        PageAnswer.NoTooltip => "no weather tooltip on the HUD right now",
        PageAnswer.NoPage => "no CoreUI1 page in the web UI yet (main menu or loading)",
        PageAnswer.Missing => "the page script is not in CoreUI1 (first visit, or the page reloaded); sending it",
        PageAnswer.NoWebView => "the game's web UI is not up yet",
        PageAnswer.NoAnswer => $"the web UI did not answer within {AnswerWaitSeconds:0}s",
        PageAnswer.InstallLeftNothing => "the page script ran but left nothing behind",
        _ when answer.StartsWith(PageAnswer.RunFailedPrefix, StringComparison.Ordinal) => "could not run script in the web UI: " + answer.Substring(PageAnswer.RunFailedPrefix.Length),
        _ => "the page answered: " + answer
    };
}
