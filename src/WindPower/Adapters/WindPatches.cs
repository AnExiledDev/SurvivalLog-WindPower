using System;
using System.Collections.Generic;
using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;
using GameCore.HotUpdate.Battle.Show;
using HarmonyLib;
using ExiledToolkit.Game;
using WindPower.Core;
using Il2CppFurnitures = Il2CppSystem.Collections.Generic.List<GameCore.HotUpdate.Battle.Logic.Furniture>;

namespace WindPower.Adapters;

/// <summary>
/// Makes the turbines' power come from the wind. Each one's power row is a solar row with no power of its own, so the
/// game lists it, saves it and draws it like a panel; these hooks add its wind power to the city's supply and show it
/// in the power panel, the power hover and the name hover. The power hover is moved down to the turbine's foot, since
/// the game puts it over the top, which is off screen when zoomed in on a tall turbine. The game's sunlight sum (CalcSolarOutput) is left alone, since
/// Power Manager already hooks it. Every target was checked in GameAssembly.dll: no relative jump or call in its first
/// 14 bytes. A hook that fails reports once and lets the game's own answer through.
/// </summary>
internal static class WindPatches
{
    private const float WindCheckSeconds = 1f;
    private const string RatedKey = "ConstantText_Text_SR_Web_GeneratorFloatPopup_1";

    /// <summary>How far above the turbine's foot the power hover points: about a person's chest.</summary>
    private const float HoverAboveFoot = 1.2f;

    private static readonly HashSet<string> Failed = new();

    /// <summary>Whether each turbine stands on the 2F terrace, by instance id; emptied with every weather read.</summary>
    private static readonly Dictionary<long, bool> OnTerrace = new();

    private static Action<string> _log = _ => { };
    private static int _weatherId = -1;
    private static string _weatherName = "";
    private static float _windReadAt = float.MinValue;
    private static int _gameSeconds;

    public static void Apply(Harmony harmony, Action<string> log)
    {
        _log = log;

        Hook(harmony, typeof(PowerManager), "CalculateGeneration", nameof(AddWindPower));
        Hook(harmony, typeof(PowerManager), "BuildSolarFloatData", nameof(ShowWindInPowerHover));
        Hook(harmony, typeof(PowerManager), "CollectFacilitiesJson", nameof(ShowWindInPowerPanel));
        Hook(harmony, typeof(Furniture), "GetElectricalHoverText", nameof(ShowWindInNameHover));
    }

    /// <summary>
    /// The wind's share of the rating now: the weather's average, risen or fallen with the hour (see
    /// <see cref="WindVariation"/>). With no weather (the main menu) it is still.
    /// </summary>
    public static float Wind()
    {
        var id = WeatherId();

        return id < 0 ? 0f : WindVariation.Share(WindOutput.Share(id), WindSettings.VariationPercent, _gameSeconds);
    }

    /// <summary>The weather's Config_Weather id and the game clock, read at most once a second; -1 when there is no weather.</summary>
    private static int WeatherId()
    {
        var now = UnityEngine.Time.realtimeSinceStartup;

        if (now - _windReadAt < WindCheckSeconds && now >= _windReadAt) return _weatherId;

        _windReadAt = now;
        _weatherId = -1;
        _weatherName = "";
        OnTerrace.Clear();

        var weather = GameSingleton.Existing<BattleLogicWorld>()?._WeatherManager;

        if (weather == null) return _weatherId;

        _weatherId = weather.GetWeatherConfigId();
        _gameSeconds = GameSingleton.Existing<BattleLogicWorld>()?._GameTimeManager?.GetTotalSeconds() ?? 0;

        var row = GameSingleton.Existing<ConfigManager>()?.Get_Config_Weather(_weatherId);

        if (row != null) _weatherName = Local(row.Name, row.Name_Local ?? "");

        return _weatherId;
    }

    /// <summary>The weather now, for the test buttons: its id (-1 when there is none) and its name.</summary>
    internal static (int Id, string Name) Weather() => (WeatherId(), _weatherName);

    /// <summary>The game clock the wind follows, in game seconds, for the test buttons' forecast.</summary>
    internal static double GameSeconds()
    {
        WeatherId();

        return _gameSeconds;
    }

    /// <summary>The wind as a whole percent of the rating, for the weather tooltip; -1 when there is no weather.</summary>
    public static int WindPercent() => WeatherId() < 0 ? -1 : WindOutput.Percent(Wind());

    private static float Power(Furniture furniture, TurbineKind kind) => WindOutput.Power(WindSettings.Of(kind).RatedPower, Wind(), IsOnTerrace(furniture));

    /// <summary>The turbine whose furniture row has this id, or null.</summary>
    private static TurbineKind? KindOf(int furnitureId)
    {
        for (var i = 0; i < TurbineRows.All.Count; i++)
            if (TurbineRows.All[i].Ids.Furniture == furnitureId) return TurbineRows.All[i];

        return null;
    }

    /// <summary>The turbine whose power row this is, or null.</summary>
    private static TurbineKind? KindOf(Config_FurnitureElectrical? row)
    {
        if (row == null) return null;

        var id = row.ID;

        for (var i = 0; i < TurbineRows.All.Count; i++)
            if (TurbineRows.All[i].Ids.Electrical == id) return TurbineRows.All[i];

        return null;
    }

    /// <summary>Whether the turbine's furniture spot is on the 2F terrace, asked once a second at most per turbine.</summary>
    private static bool IsOnTerrace(Furniture furniture)
    {
        var id = furniture.InstanceId;

        if (OnTerrace.TryGetValue(id, out var isOnTerrace)) return isOnTerrace;

        var slot = AgentTools.GetAgentComponent<SlotComponent>(furniture)?.SlotName;
        var config = GameSingleton.Existing<ConfigManager>();

        isOnTerrace = !string.IsNullOrEmpty(slot) && config != null && config.GetMapSlotRoomByName(slot) == TurbineRows.TerraceRoom;
        OnTerrace[id] = isOnTerrace;

        return isOnTerrace;
    }

    private static void AddWindPower(Il2CppFurnitures __0, ref float __result)
    {
        try
        {
            if (__0 == null) return;

            for (var i = 0; i < __0.Count; i++)
                if (__0[i] is { } furniture && KindOf(furniture.AgentConfigId) is { } kind) __result += Power(furniture, kind);
        }
        catch (Exception ex)
        {
            Fail("power supply", ex);
        }
    }

    private static void ShowWindInPowerHover(Furniture __0, Config_FurnitureElectrical __2, SolarFloatData __result)
    {
        TurbineKind? kind;

        try
        {
            kind = KindOf(__2);
        }
        catch (Exception ex)
        {
            Fail("power hover", ex);
            return;
        }

        try
        {
            if (kind == null || __result == null || __0 == null) return;

            var rated = WindSettings.Of(kind).RatedPower;
            var share = Wind();
            var isOnTerrace = IsOnTerrace(__0);
            var note = WindOutput.Note(Local(RatedKey, "Rated {0}"), Local(TurbineRows.WindNoteKey, "{0} wind {1}"), Local(TurbineRows.TerraceNoteKey, "2F terrace +20%"), _weatherName, rated, share, StrengthWord(WindOutput.Percent(share)), isOnTerrace);

            __result.PowerCur = WindOutput.Shown(WindOutput.Power(rated, share, isOnTerrace));
            __result.NoteText = note;
            __result.IsIdle = false;
        }
        catch (Exception ex)
        {
            Fail("power hover", ex);
        }

        try
        {
            if (kind != null && __result != null && __0 != null) PointHoverAtFoot(__0, __result);
        }
        catch (Exception ex)
        {
            Fail("power hover's position", ex);
        }
    }

    /// <summary>Where the game's own sum puts the hover, measured from the turbine's foot instead of its head bar.</summary>
    private static void PointHoverAtFoot(Furniture furniture, SolarFloatData hover)
    {
        var model = GameSingleton.Existing<BattleShowWorld>()?._AgentModelManager?.GetAgentModelByLogicInstanceId(furniture.InstanceId);
        var foot = model?.Obj;
        var camera = UnityEngine.Camera.main;

        if (foot == null || camera == null) return;

        var point = camera.WorldToViewportPoint(foot.transform.position + UnityEngine.Vector3.up * HoverAboveFoot);

        if (point.z <= 0f) return;

        hover.ScreenX = Math.Clamp(point.x, 0f, 1f);
        hover.ScreenY = Math.Clamp(1f - point.y, 0f, 1f);
    }

    private static void ShowWindInPowerPanel(ref int __0, ref string __result)
    {
        try
        {
            if (string.IsNullOrEmpty(__result)) return;

            var agents = GameSingleton.Existing<BattleLogicWorld>()?._AgentManager;

            if (agents == null) return;

            var powerById = new Dictionary<string, int>();

            foreach (var kind in TurbineRows.All)
            {
                var turbines = agents.GetFurnituresByConfigId(kind.Ids.Furniture);

                if (turbines == null) continue;

                for (var i = 0; i < turbines.Count; i++)
                    if (turbines[i] is { } turbine) powerById[turbine.InstanceId.ToString()] = WindOutput.Shown(Power(turbine, kind));
            }

            if (powerById.Count == 0) return;

            var (json, added) = WindOutput.Rewrite(__result, powerById);

            __result = json;
            __0 += added;
        }
        catch (Exception ex)
        {
            Fail("power panel", ex);
        }
    }

    private static void ShowWindInNameHover(Furniture __instance, Config_FurnitureElectrical __0, ref string __result)
    {
        try
        {
            if (KindOf(__0) is not { } kind || __instance == null) return;

            __result = string.Format(GameKey.Text_Power_HoverGeneration, (float)WindOutput.Shown(Power(__instance, kind)));
        }
        catch (Exception ex)
        {
            Fail("name hover", ex);
        }
    }

    /// <summary>The game's text for <paramref name="key"/> in the player's language, or <paramref name="fallback"/> when it has none.</summary>
    /// <summary>The game-language word for a wind of this percent.</summary>
    internal static string StrengthWord(int percent)
    {
        var word = WindOutput.Strength(percent);

        return Local(word.Key, word.En);
    }

    internal static string Local(string key, string fallback)
    {
        var text = string.IsNullOrEmpty(key) ? null : ConstantTextTools.GetLocalText(key);

        return string.IsNullOrEmpty(text) || text == key ? fallback : text;
    }

    private static void Hook(Harmony harmony, Type type, string method, string postfix)
    {
        try
        {
            var target = AccessTools.Method(type, method);

            if (target == null)
            {
                _log($"cannot find {type.Name}.{method}, so the turbine's wind power is missing from one place");

                return;
            }

            harmony.Patch(target, postfix: new HarmonyMethod(typeof(WindPatches), postfix));
        }
        catch (Exception ex)
        {
            _log($"cannot hook {type.Name}.{method}: {ex.Message}");
        }
    }

    private static void Fail(string where, Exception ex)
    {
        if (Failed.Add(where)) _log($"the turbine's wind power in the {where} failed (reported once): {ex}");
    }
}
