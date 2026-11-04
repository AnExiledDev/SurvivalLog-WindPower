using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using WindPower.Adapters;

namespace WindPower;

[BepInPlugin(Guid, "Wind Power", Version)]
public sealed class Plugin : BasePlugin
{
    private const string Guid = "com.dev.survivallog.windpower";
    private const string Version = "0.2.0";

    public override void Load()
    {
        var tables = new TurbineTables(message => Log.LogInfo(message));
        var harmony = new Harmony(Guid);
        var iconPlaced = false;

        WindPatches.Apply(harmony, message => Log.LogWarning(message));
        SavePatches.Apply(harmony, message => Log.LogWarning(message));
        TurbineModel.Apply(harmony, message => Log.LogInfo(message));

        WindTicker.Log = message => Log.LogError(message);
        WindTicker.Check = now =>
        {
            if (!iconPlaced)
            {
                iconPlaced = true;
                Log.LogInfo(TurbineIcon.Place());
            }

            tables.Tick(now);
            TurbineModel.Spin();
        };
        AddComponent<WindTicker>();
        Log.LogInfo($"Wind Power {Version} loaded");
    }
}
