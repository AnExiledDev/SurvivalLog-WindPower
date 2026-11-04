using Jint;

namespace WindPower.Tests;

/// <summary>
/// windpower.js exactly as the plugin embeds it, run in Jint on top of FakePage.js, a page with only the parts of
/// CoreUI1 the script touches. Tests talk to it in JavaScript expressions and read back plain strings, numbers and
/// booleans.
/// </summary>
internal sealed class PageScript
{
    private readonly Engine _engine = new();

    private PageScript(string pageSpec)
    {
        _engine.Execute(Resource("WindPower.Tests.FakePage.js"));
        _engine.Execute("var page = buildPage(" + pageSpec + ");");
        _engine.Execute(Resource("WindPower.windpower.js"));
    }

    /// <summary>The script in a page drawn from a buildPage spec; the page is the global <c>page</c>.</summary>
    public static PageScript On(string pageSpec = "{}") => new(pageSpec);

    /// <summary>What the plugin's check call does once it has found the frame, with a reading from <see cref="Core.PageCalls.Reading"/>.</summary>
    public string Apply(string reading) => Text($"window.__slwind.apply({reading})");

    /// <summary>The script sent again, as the plugin does when the page says it is missing.</summary>
    public void Reinstall() => _engine.Execute(Resource("WindPower.windpower.js"));

    public void Run(string statements) => _engine.Execute(statements);

    public string Text(string expression) => _engine.Evaluate($"String({expression})").AsString();

    public bool Is(string expression) => _engine.Evaluate($"!!({expression})").AsBoolean();

    public double Number(string expression) => _engine.Evaluate($"Number({expression})").AsNumber();

    private static string Resource(string name)
    {
        using var stream = typeof(PageScript).Assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException(name + " is not embedded in the test assembly");
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }
}
