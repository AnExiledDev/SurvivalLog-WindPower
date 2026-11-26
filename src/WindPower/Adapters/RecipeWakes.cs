using GameCore.HotUpdate.Battle.Logic;
using ExiledToolkit.Game;

namespace WindPower.Adapters;

/// <summary>
/// Wakes the turbine recipe check when the game may have shown a recipe. ToolTableManager keeps the shown recipes,
/// and only these five write them: Initialize and LoadGame when a run starts or a save loads, AddProductionRecords and
/// AddProductionCraftUnlocks when a recipe is shown or unlocked, and TryCreateItem when something is crafted. If a hook
/// cannot go in, the check runs every few seconds instead. All five targets were checked in GameAssembly.dll: no
/// relative jump or call in their first 14 bytes.
/// </summary>
internal static class RecipeWakes
{
    /// <summary>How many wakes there have been.</summary>
    public static int Count { get; private set; }

    public static bool IsPolling { get; private set; }

    public static void Apply(PatchKit hooks)
    {
        const string consequence = "the turbine recipes are checked every 5 seconds instead";

        var isPlaced = true;

        foreach (var method in new[] { "Initialize", "LoadGame", "AddProductionRecords", "AddProductionCraftUnlocks", "TryCreateItem" })
            isPlaced &= hooks.Patch(typeof(RecipeWakes), typeof(ToolTableManager), method, postfix: nameof(Wake), consequence: consequence);

        if (!isPlaced) IsPolling = true;
    }

    private static void Wake() => Count++;
}
