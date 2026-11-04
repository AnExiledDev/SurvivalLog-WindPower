using GameCore.HotUpdate;

namespace WindPower.Adapters;

/// <summary>
/// The game's singletons build themselves on first read, and one read before the game makes it is built before the
/// configs load and breaks halfway. So the mod only reads what the game already made.
/// </summary>
internal static class GameSingleton
{
    public static T? Existing<T>() where T : BaseSingleton<T>, new() =>
        BaseSingleton<T>.IsInstanceCreated ? BaseSingleton<T>.Instance : null;
}
