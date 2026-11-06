using System;

namespace WindPower.Core;

/// <summary>
/// How a shop remembers the stock setting it last took for each turbine, so a setting changed while the shop was not
/// loaded still reaches it. The shop's own stock dictionary is saved with the run, so the setting goes in there too,
/// under the turbine's furniture id made negative, which no furniture has. The value is negative as well: the game's
/// sell-out event only touches counts above 0, and the shop lists only the furniture its config row names, so the
/// game never shows or changes the mark.
/// </summary>
public static class ShopMarks
{
    /// <summary>The stock key that holds the setting for this turbine's furniture id.</summary>
    public static int Key(int furniture) => -furniture;

    public static int Encode(int setting) => -Math.Max(0, setting) - 1;

    /// <summary>The setting a mark holds, or null for a value that is not a mark.</summary>
    public static int? Decode(int value) => value < 0 ? -value - 1 : null;

    /// <summary>
    /// A shop's count of one turbine once the stock setting is <paramref name="listed"/>. <paramref name="current"/> is
    /// its count now (null when the shop has none listed) and <paramref name="applied"/> the setting it last took (null
    /// in a save from before the marks). A count moves by the change in the setting, so turbines already bought stay
    /// bought; with no mark it can only come down to the setting. Null means the shop still does not list it.
    /// </summary>
    public static int? Count(int? current, int? applied, int listed)
    {
        if (current is not int count) return listed > 0 ? listed : null;

        if (applied is not int before) return Math.Min(count, Math.Max(0, listed));

        return Unlocks.NewStock(count, before, listed);
    }
}
