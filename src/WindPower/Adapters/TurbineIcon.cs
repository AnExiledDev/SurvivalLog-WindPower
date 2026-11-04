using System;
using System.IO;
using System.Linq;
using UnityEngine;
using WindPower.Core;

namespace WindPower.Adapters;

/// <summary>
/// Puts the turbine's icon where the game's web UI looks for pictures, next to the game's own furniture icons. It is
/// written only when the file there is missing or differs, so a mod update brings its new icon.
/// </summary>
internal static class TurbineIcon
{
    /// <summary>Call on the main thread, since Unity gives the folder's path only there. Returns what happened.</summary>
    public static string Place()
    {
        try
        {
            var folder = Path.Combine(Application.streamingAssetsPath, "WebUI", TurbineRows.WebIconFolder);
            var file = Path.Combine(folder, TurbineRows.IconFile);
            var icon = TurbineModel.Resource(TurbineRows.IconFile);

            if (File.Exists(file) && File.ReadAllBytes(file).SequenceEqual(icon)) return "the turbine's icon is in place";

            Directory.CreateDirectory(folder);
            File.WriteAllBytes(file, icon);

            return "the turbine's icon was written to " + file;
        }
        catch (Exception ex)
        {
            return "the turbine's icon could not be written, so it shows without one: " + ex.Message;
        }
    }
}
