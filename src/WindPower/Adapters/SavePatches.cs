using System;
using System.Collections.Generic;
using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;
using HarmonyLib;
using WindPower.Core;
using Il2CppAgentSaves = Il2CppSystem.Collections.Generic.List<GameCore.HotUpdate.AgentSave>;

namespace WindPower.Adapters;

/// <summary>
/// Saves each turbine as the stock solar panel it copies and turns it back on load (see <see cref="SaveTwin"/>). The
/// furniture and packed-furniture saves are hooked after they write their ids; the load is hooked where the game
/// gathers a chapter's saves before creating them; moving furniture between chapters clones a save without its name,
/// so the clone gets the marker back. Every target was checked in GameAssembly.dll: no relative jump or call in its
/// first 14 bytes.
/// </summary>
internal static class SavePatches
{
    private static readonly HashSet<string> Failed = new();
    private static Action<string> _log = _ => { };

    public static void Apply(Harmony harmony, Action<string> log)
    {
        _log = log;

        Hook(harmony, typeof(Furniture), "SaveGame", postfix: nameof(SaveAsStock));
        Hook(harmony, typeof(BagFurniture), "SaveGame", postfix: nameof(SaveAsStock));
        Hook(harmony, typeof(AgentManager), "DeduplicateAgentSavesForLoad", prefix: nameof(LoadAsTurbine));
        Hook(harmony, typeof(GameSaveManager), "CloneAgentSave", postfix: nameof(KeepMarkerOnClone));
    }

    private static void SaveAsStock(AgentSave __0)
    {
        try
        {
            if (__0 == null) return;

            var fields = Read(__0);

            if (SaveTwin.ToStock(ref fields, TurbineRows.All)) Write(__0, fields);
        }
        catch (Exception ex)
        {
            Fail("save", ex);
        }
    }

    private static void LoadAsTurbine(Il2CppAgentSaves __0)
    {
        try
        {
            if (__0 == null) return;

            for (var i = 0; i < __0.Count; i++)
            {
                var save = __0[i];

                if (save == null) continue;

                var fields = Read(save);

                if (SaveTwin.FromStock(ref fields, TurbineRows.All)) Write(save, fields);
            }
        }
        catch (Exception ex)
        {
            Fail("load", ex);
        }
    }

    private static void KeepMarkerOnClone(AgentSave __0, AgentSave __result)
    {
        try
        {
            if (__0?.Name != null && __result != null && __0.Name.StartsWith(SaveTwin.MarkerStart, StringComparison.Ordinal)) __result.Name = __0.Name;
        }
        catch (Exception ex)
        {
            Fail("chapter move", ex);
        }
    }

    private static SaveTwin.Fields Read(AgentSave save) => new()
    {
        AgentConfigId = save.AgentConfigId,
        IsBagFurniture = save.IsBagFurniture,
        BagFurnitureConfigId = save.BagFurnitureConfigId,
        Name = save.Name,
    };

    private static void Write(AgentSave save, SaveTwin.Fields fields)
    {
        save.AgentConfigId = fields.AgentConfigId;
        save.BagFurnitureConfigId = fields.BagFurnitureConfigId;
        save.Name = fields.Name;
    }

    private static void Hook(Harmony harmony, Type type, string method, string? prefix = null, string? postfix = null)
    {
        try
        {
            var target = AccessTools.Method(type, method);

            if (target == null)
            {
                _log($"cannot find {type.Name}.{method}, so turbines may not save as solar panels");

                return;
            }

            harmony.Patch(
                target,
                prefix: prefix == null ? null : new HarmonyMethod(typeof(SavePatches), prefix),
                postfix: postfix == null ? null : new HarmonyMethod(typeof(SavePatches), postfix));
        }
        catch (Exception ex)
        {
            _log($"cannot hook {type.Name}.{method}: {ex.Message}");
        }
    }

    private static void Fail(string where, Exception ex)
    {
        if (Failed.Add(where)) _log($"the turbine's {where} step failed (reported once): {ex}");
    }
}
