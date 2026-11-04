using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using WindPower.Adapters;
using WindPower.Core;

namespace WindPower;

[BepInPlugin(Guid, "Wind Power", Version)]
[BepInDependency(OptionalLive.ToolkitGuid, BepInDependency.DependencyFlags.SoftDependency)]
public sealed class Plugin : BasePlugin
{
    private const string Guid = "com.dev.survivallog.windpower";
    private const string Version = "0.6.2";

    public override void Load()
    {
        if (!OptionalLive.IsToolkitInstalled) Log.LogInfo(OptionalLive.MissingNote);

        _uninstallPrep = Config.Bind("Uninstall", "UninstallPrep", false,
            "Turn this on before you remove Wind Power. Once a save is loaded, every Wind Turbine Package becomes the Solar Panel Package of its size, in your bags, in storage and on the floor, in every place the save holds, and the shops stop selling the turbines. Placed and packed turbines are always saved as solar panels, so they need nothing. Then save the game, quit, and remove WindPower.dll. LogOutput.log lists what was changed. Turning this off again does not turn the packages back.\n" +
            "移除风力发电模组之前请先打开此项。读取存档后，背包、储物和地上的每个风力发电机包裹都会变成同尺寸的太阳能发电板包裹，存档里的每个地点都一样，商店也不再出售风力发电机。已安装和已打包的风力发电机本来就按太阳能发电板保存，不受影响。然后存档、退出游戏，再删除 WindPower.dll。改了哪些东西写在 LogOutput.log 里。再关掉此项，包裹也不会变回来。");

        BindSettings(TurbineRows.Small, "Small Solar Panel", "小型太阳能发电板", 20);
        BindSettings(TurbineRows.Medium, "Medium Solar Panel", "中型太阳能发电板", 40);
        BindSettings(TurbineRows.Large, "Large Solar Panel", "大型太阳能发电板", 80);
        OptionalLive.Watch(_uninstallPrep, isOn =>
        {
            UninstallPrep.Set(isOn);
            if (isOn) Log.LogInfo("Uninstall prep is on: load a save to turn the turbine packages into solar panel packages");
        });

        var tables = new TurbineTables(message => Log.LogInfo(message));
        var weatherPage = new WeatherPage(message => Log.LogInfo(message));
        var harmony = new Harmony(Guid);
        var iconPlaced = false;

        WindPatches.Apply(harmony, message => Log.LogWarning(message));
        SavePatches.Apply(harmony, message => Log.LogWarning(message));
        ShopStock.Apply(harmony, message => Log.LogWarning(message));
        TurbineModel.Apply(harmony, message => Log.LogInfo(message));

        WindTicker.Log = message => Log.LogError(message);
        UninstallPrep.Log = message => Log.LogInfo(message);
        WindTicker.Check = now =>
        {
            if (!iconPlaced)
            {
                iconPlaced = true;
                Log.LogInfo(TurbineIcon.Place());
            }

            tables.Tick(now);
            UninstallPrep.Tick(now);
            TurbineModel.Animate();
            weatherPage.Tick(now);
        };
        AddComponent<WindTicker>();
        Log.LogInfo($"Wind Power {Version} loaded");
    }

    /// <summary>One section of settings per turbine. Every one applies live: the turbine's rows are rewritten when one changes.</summary>
    private void BindSettings(TurbineKind kind, string solar, string solarChinese, int solarPrice)
    {
        var section = kind.Size + "Turbine";
        var name = kind.Name;
        var chinese = kind.Texts.First(text => text.Key == kind.Key("Name")).Zh;
        var defaults = kind.Defaults;

        var rated = Config.Bind(section, "RatedPower", defaults.RatedPower, new ConfigDescription(
            $"Power the {name} makes in the strongest wind. Calmer weather gives a share of it, and a turbine on the 2F terrace makes 20% more.\n" +
            $"{chinese}在风最大时的发电量。风小的天气只发出其中一部分，装在二楼露台的多发 20%。",
            new AcceptableValueRange<float>(1f, 200f)));
        var materials = Config.Bind(section, "Materials", defaults.Materials,
            $"What crafting a {name} Package takes, written as item id:count pairs split by commas. 20004 is Scrap Metal, 20104 Wire, 20210 Electronic Components and 20373 Stainless Steel Plate. Up to {MaterialList.MostKinds} different items. A list the game cannot use (an unknown item, or the same materials as another recipe) falls back to the default, and LogOutput.log says why.\n" +
            $"制作{chinese}包裹所需的材料，写成「物品id:数量」，用逗号隔开。20004 是铁皮，20104 是铁丝，20210 是电器元件，20373 是不锈钢板。最多 {MaterialList.MostKinds} 种物品。游戏用不了的清单（物品不存在，或和别的配方材料相同）会改用默认值，原因写在 LogOutput.log 里。");
        var price = Config.Bind(section, "ShopPrice", defaults.ShopPrice, new ConfigDescription(
            $"What the Classic Furniture shop asks for a {name} before the disaster. The {solar} costs {solarPrice} there.\n" +
            $"灾难前「经典家具综合」商店卖{chinese}的价格。{solarChinese}在那里卖 {solarPrice}。",
            new AcceptableValueRange<int>(0, 9999)));
        var stock = Config.Bind(section, "ShopStock", defaults.ShopStock, new ConfigDescription(
            $"How many {name}s the Classic Furniture shop has for sale before the disaster. 0 takes them off the shelves. A shop that already has more than this is brought down to it; turbines already bought stay yours.\n" +
            $"灾难前「经典家具综合」商店有几台{chinese}出售。0 表示不卖。已有更多存货时会减到这个数；已经买下的不受影响。",
            new AcceptableValueRange<int>(0, 20)));

        // Uninstall prep holds the stock at 0, so the shops stop listing the turbine.
        OptionalLive.Watch(() => WindSettings.Set(kind, new TurbineSettings(rated.Value, materials.Value, price.Value, _uninstallPrep!.Value ? 0 : stock.Value)), rated, materials, price, stock, _uninstallPrep!);
    }

    private ConfigEntry<bool>? _uninstallPrep;
}
