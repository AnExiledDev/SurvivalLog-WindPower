using System;
using System.Collections.Generic;
using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;
using HarmonyLib;
using WindPower.Core;
using Il2CppFurnitures = Il2CppSystem.Collections.Generic.List<GameCore.HotUpdate.Battle.Logic.Furniture>;

namespace WindPower.Adapters;

/// <summary>
/// Makes the turbine's power come from the wind. Its power row is a solar row with no power of its own, so the game
/// lists it, saves it and draws it like a panel; these hooks add its wind power to the city's supply and show it in
/// the power panel, the power hover and the name hover. The game's sunlight sum (CalcSolarOutput) is left alone, since
/// Power Manager already hooks it. Every target was checked in GameAssembly.dll: no relative jump or call in its first
/// 14 bytes. A hook that fails reports once and lets the game's own answer through.
/// </summary>
internal static class WindPatches
{
    private const float WindCheckSeconds = 1f;
    private const string RatedKey = "ConstantText_Text_SR_Web_GeneratorFloatPopup_1";

    private static readonly HashSet<string> Failed = new();
    private static Action<string> _log = _ => { };
    private static float _wind;
    private static string _weatherName = "";
    private static float _windReadAt = float.MinValue;

    public static void Apply(Harmony harmony, Action<string> log)
    {
        _log = log;

        Hook(harmony, typeof(PowerManager), "CalculateGeneration", nameof(AddWindPower));
        Hook(harmony, typeof(PowerManager), "BuildSolarFloatData", nameof(ShowWindInPowerHover));
        Hook(harmony, typeof(PowerManager), "CollectFacilitiesJson", nameof(ShowWindInPowerPanel));
        Hook(harmony, typeof(Furniture), "GetElectricalHoverText", nameof(ShowWindInNameHover));
    }

    /// <summary>The wind the game's weather gives now, read at most once a second, and the weather's name.</summary>
    public static float Wind(out string weatherName)
    {
        var now = UnityEngine.Time.realtimeSinceStartup;

        if (now - _windReadAt >= WindCheckSeconds || now < _windReadAt)
        {
            _windReadAt = now;
            _wind = 0f;
            _weatherName = "";

            var weather = GameSingleton.Existing<BattleLogicWorld>()?._WeatherManager;
            var row = weather == null ? null : GameSingleton.Existing<ConfigManager>()?.Get_Config_Weather(weather.GetWeatherConfigId());

            if (row != null)
            {
                _wind = row.WindValue;
                _weatherName = Local(row.Name, row.Name_Local ?? "");
            }
        }

        weatherName = _weatherName;

        return _wind;
    }

    private static float Power() => WindOutput.Power(Wind(out _));

    private static bool IsTurbine(Config_FurnitureElectrical? row) => row != null && row.ID == TurbineRows.Ids.Electrical;

    private static void AddWindPower(Il2CppFurnitures __0, ref float __result)
    {
        try
        {
            if (__0 == null) return;

            var turbines = 0;

            for (var i = 0; i < __0.Count; i++)
                if (__0[i]?.AgentConfigId == TurbineRows.Ids.Furniture) turbines++;

            if (turbines > 0) __result += turbines * Power();
        }
        catch (Exception ex)
        {
            Fail("power supply", ex);
        }
    }

    private static void ShowWindInPowerHover(Config_FurnitureElectrical __2, SolarFloatData __result)
    {
        try
        {
            if (!IsTurbine(__2) || __result == null) return;

            var wind = Wind(out var weatherName);
            var power = WindOutput.Power(wind);

            __result.PowerCur = WindOutput.Shown(power);
            __result.NoteText = WindOutput.Note(Local(RatedKey, "Rated {0}"), Local(TurbineRows.WindNoteKey, "{0} wind {1}"), weatherName, power);
            __result.IsIdle = false;
        }
        catch (Exception ex)
        {
            Fail("power hover", ex);
        }
    }

    private static void ShowWindInPowerPanel(ref int __0, ref string __result)
    {
        try
        {
            if (string.IsNullOrEmpty(__result)) return;

            var turbines = GameSingleton.Existing<BattleLogicWorld>()?._AgentManager?.GetFurnituresByConfigId(TurbineRows.Ids.Furniture);

            if (turbines == null || turbines.Count == 0) return;

            var shown = WindOutput.Shown(Power());
            var powerById = new Dictionary<string, int>();

            for (var i = 0; i < turbines.Count; i++)
                if (turbines[i] != null) powerById[turbines[i].InstanceId.ToString()] = shown;

            var (json, added) = WindOutput.Rewrite(__result, powerById);

            __result = json;
            __0 += added;
        }
        catch (Exception ex)
        {
            Fail("power panel", ex);
        }
    }

    private static void ShowWindInNameHover(Config_FurnitureElectrical __0, ref string __result)
    {
        try
        {
            if (!IsTurbine(__0)) return;

            __result = string.Format(GameKey.Text_Power_HoverGeneration, (float)WindOutput.Shown(Power()));
        }
        catch (Exception ex)
        {
            Fail("name hover", ex);
        }
    }

    /// <summary>The game's text for <paramref name="key"/> in the player's language, or <paramref name="fallback"/> when it has none.</summary>
    private static string Local(string key, string fallback)
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
