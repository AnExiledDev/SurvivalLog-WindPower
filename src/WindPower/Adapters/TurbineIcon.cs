using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using WindPower.Core;

namespace WindPower.Adapters;

/// <summary>
/// Puts each turbine's icon where the game's web UI looks for pictures, next to the game's own furniture icons. An icon
/// is written only when the file there is missing or differs, so a mod update brings its new icon.
/// </summary>
internal static class TurbineIcon
{
    /// <summary>Call on the main thread, since Unity gives the folder's path only there. Returns what happened.</summary>
    public static string Place()
    {
        var folder = Path.Combine(Application.streamingAssetsPath, "WebUI", TurbineRows.WebIconFolder);
        var notes = new List<string>();

        foreach (var kind in TurbineRows.All) notes.Add(Place(folder, kind));

        return string.Join("; ", notes);
    }

    private static string Place(string folder, TurbineKind kind)
    {
        try
        {
            var file = Path.Combine(folder, kind.IconFile);
            var icon = TurbineModel.Resource(kind.IconFile);

            if (File.Exists(file) && File.ReadAllBytes(file).SequenceEqual(icon)) return $"the {kind.Name}'s icon is in place";

            Directory.CreateDirectory(folder);
            File.WriteAllBytes(file, icon);

            return $"the {kind.Name}'s icon was written to {file}";
        }
        catch (Exception ex)
        {
            return $"the {kind.Name}'s icon could not be written, so it shows without one: {ex.Message}";
        }
    }
}
