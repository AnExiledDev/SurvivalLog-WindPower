namespace WindPower.Core;

/// <summary>What the page calls answer. Anything else the page says is passed on to the log as it came.</summary>
public static class PageAnswer
{
    /// <summary>The wind row is in the weather tooltip (or taken out, when there is no weather) as the reading says.</summary>
    public const string Ok = "ok";

    /// <summary>The script is in the page, but the HUD has no weather tooltip right now.</summary>
    public const string NoTooltip = "notooltip";

    /// <summary>Root.html has no CoreUI1 frame: the main menu, or a load in progress.</summary>
    public const string NoPage = "nopage";

    /// <summary>The CoreUI1 frame is there but windpower.js is not: the first visit, or the page reloaded.</summary>
    public const string Missing = "missing";

    /// <summary>The script ran but did not leave its entry point behind.</summary>
    public const string InstallLeftNothing = "install left no script";

    /// <summary>Not from the page: the game's web view is not up yet, so nothing was sent.</summary>
    public const string NoWebView = "no web view";

    /// <summary>Not from the page: nothing came back in time.</summary>
    public const string NoAnswer = "no answer";

    /// <summary>Not from the page: starts every answer where the web view threw instead of taking the call; the rest is its message.</summary>
    public const string RunFailedPrefix = "run failed: ";
}
