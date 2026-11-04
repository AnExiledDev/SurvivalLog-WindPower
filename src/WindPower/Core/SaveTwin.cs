using System;
using System.Collections.Generic;

namespace WindPower.Core;

/// <summary>
/// A turbine is saved as the stock solar panel it copies, so a save still loads after the mod is removed (the game
/// cannot create a furniture whose id it does not know). The furniture's save has a name field the game only uses for
/// the player, so the turbine writes a marker there saying which turbine it is and which ids it swapped; on load with
/// the mod present those ids turn back. Without the mod the marker is ignored and the player has a solar panel.
/// </summary>
public static class SaveTwin
{
    /// <summary>What every turbine's marker starts with.</summary>
    public const string MarkerStart = "WindPower.Turbine";

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

    /// <summary>Swaps a turbine's ids for its stock twin's and marks them. False when there was nothing of ours to swap.</summary>
    public static bool ToStock(ref Fields save, IReadOnlyList<TurbineKind> kinds)
    {
        if (!string.IsNullOrEmpty(save.Name)) return false;

        foreach (var kind in kinds)
        {
            var swapped = "";

            if (save.AgentConfigId == kind.Ids.Furniture)
            {
                save.AgentConfigId = kind.Stock.Furniture;
                swapped += Placed;
            }

            if (save.IsBagFurniture && save.BagFurnitureConfigId == kind.Ids.Furniture)
            {
                save.BagFurnitureConfigId = kind.Stock.Furniture;
                swapped += Packed;
            }

            if (swapped.Length == 0) continue;

            save.Name = kind.SaveMarker + swapped;

            return true;
        }

        return false;
    }

    /// <summary>Turns marked stock ids back into the turbine's and clears the marker. False when the save is not a marked turbine.</summary>
    public static bool FromStock(ref Fields save, IReadOnlyList<TurbineKind> kinds)
    {
        if (save.Name == null) return false;

        foreach (var kind in kinds)
        {
            if (!save.Name.StartsWith(kind.SaveMarker, StringComparison.Ordinal)) continue;

            var swapped = save.Name.Substring(kind.SaveMarker.Length);

            if (swapped.IndexOf(Placed) >= 0 && save.AgentConfigId == kind.Stock.Furniture) save.AgentConfigId = kind.Ids.Furniture;

            if (swapped.IndexOf(Packed) >= 0 && save.IsBagFurniture && save.BagFurnitureConfigId == kind.Stock.Furniture) save.BagFurnitureConfigId = kind.Ids.Furniture;

            save.Name = null;

            return true;
        }

        return false;
    }
}
