using System.Threading;

namespace WindPower.Core;

/// <summary>
/// Holds the page's answer to the call now out. The web view sometimes answers after the watch has given up on a call
/// and sent the next one; each call gets a number, and an answer to an older call is dropped instead of being taken
/// for the newer call's answer. Safe to offer to from the web view's callback.
/// </summary>
public sealed class ReplySlot
{
    private int _current;
    private string? _reply;

    /// <summary>Numbers the call about to go out and drops anything an earlier call left behind.</summary>
    public int Open()
    {
        var call = Interlocked.Increment(ref _current);
        Interlocked.Exchange(ref _reply, null);

        return call;
    }

    public void Offer(int call, string reply)
    {
        if (call == Volatile.Read(ref _current)) Interlocked.Exchange(ref _reply, reply);
    }

    /// <summary>The answer to the call now out, handed over once; null while none has come.</summary>
    public string? Take() => Interlocked.Exchange(ref _reply, null);
}
