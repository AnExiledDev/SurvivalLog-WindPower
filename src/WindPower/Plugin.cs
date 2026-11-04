using BepInEx;
using BepInEx.Unity.IL2CPP;
using WindPower.Adapters;

namespace WindPower;

[BepInPlugin(Guid, "Wind Power", Version)]
public sealed class Plugin : BasePlugin
{
    private const string Guid = "com.dev.survivallog.windpower";
    private const string Version = "0.1.0";

    public override void Load()
    {
        var tables = new TurbineTables(message => Log.LogInfo(message));

        WindTicker.Log = message => Log.LogError(message);
        WindTicker.Check = tables.Tick;
        AddComponent<WindTicker>();
        Log.LogInfo($"Wind Power {Version} loaded");
    }
}
