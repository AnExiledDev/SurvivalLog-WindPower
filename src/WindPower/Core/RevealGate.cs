namespace WindPower.Core;

/// <summary>
/// Says when to check the turbines' recipes: once at the start, then after each wake (the game showed or unlocked a
/// recipe, or loaded a save) or after <see cref="Again"/>. If a hook could not go in, every few seconds instead.
/// </summary>
public sealed class RevealGate
{
    private readonly double _pollSeconds;
    private int _seenWakes;
    private bool _isOwed = true;
    private double _nextPollAt = double.MinValue;

    public RevealGate(double pollSeconds)
    {
        _pollSeconds = pollSeconds;
    }

    /// <summary>Checks again at the next tick, for when the list of recipes to show changed.</summary>
    public void Again() => _isOwed = true;

    /// <summary>True when the recipes should be checked now. <paramref name="wakes"/> is the wake count so far.</summary>
    public bool IsDue(double now, int wakes, bool isPolling)
    {
        if (isPolling)
        {
            if (now < _nextPollAt) return false;

            _nextPollAt = now + _pollSeconds;

            return true;
        }

        if (!_isOwed && wakes == _seenWakes) return false;

        _isOwed = false;
        _seenWakes = wakes;

        return true;
    }
}
