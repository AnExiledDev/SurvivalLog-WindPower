using System;

namespace WindPower.Core;

/// <summary>
/// A turbine is saved as the stock Large Solar Panel, so a save still loads after the mod is removed (the game cannot
/// create a furniture whose id it does not know). The furniture's save has a name field the game only uses for the
/// player, so the turbine writes a marker there saying which ids it swapped; on load with the mod present those ids
/// turn back. Without the mod the marker is ignored and the player has a solar panel.
/// </summary>
public static class SaveTwin
{
    public const string Marker = "WindPower.Turbine:";
    private const char Placed = 'a';
    private const char Packed = 'b';

    /// <summary>The fields of one furniture save the swap reads and writes.</summary>
    public struct Fields
    {
        public int AgentConfigId;
        public bool IsBagFurniture;
        public int BagFurnitureConfigId;
        public string? Name;
    }

    /// <summary>Swaps the turbine's ids for the stock twin's and marks them. False when there was nothing of ours to swap.</summary>
    public static bool ToStock(ref Fields save)
    {
        if (!string.IsNullOrEmpty(save.Name)) return false;

        var swapped = "";

        if (save.AgentConfigId == TurbineRows.Ids.Furniture)
        {
            save.AgentConfigId = TurbineRows.Stock.Furniture;
            swapped += Placed;
        }

        if (save.IsBagFurniture && save.BagFurnitureConfigId == TurbineRows.Ids.Furniture)
        {
            save.BagFurnitureConfigId = TurbineRows.Stock.Furniture;
            swapped += Packed;
        }

        if (swapped.Length == 0) return false;

        save.Name = Marker + swapped;

        return true;
    }

    /// <summary>Turns marked stock ids back into the turbine's and clears the marker. False when the save is not a marked turbine.</summary>
    public static bool FromStock(ref Fields save)
    {
        if (save.Name == null || !save.Name.StartsWith(Marker, StringComparison.Ordinal)) return false;

        var swapped = save.Name.Substring(Marker.Length);

        if (swapped.IndexOf(Placed) >= 0 && save.AgentConfigId == TurbineRows.Stock.Furniture) save.AgentConfigId = TurbineRows.Ids.Furniture;

        if (swapped.IndexOf(Packed) >= 0 && save.IsBagFurniture && save.BagFurnitureConfigId == TurbineRows.Stock.Furniture) save.BagFurnitureConfigId = TurbineRows.Ids.Furniture;

        save.Name = null;

        return true;
    }
}
