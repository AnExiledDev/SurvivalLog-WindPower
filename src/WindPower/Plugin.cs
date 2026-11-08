using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using ExiledToolkit;
using HarmonyLib;
using WindPower.Adapters;
using WindPower.Core;

namespace WindPower;

[BepInPlugin(Guid, "Wind Power", Version)]
[BepInDependency("com.dev.survivallog.exiledtoolkit", ">=0.19.0")]
public sealed class Plugin : BasePlugin
{
    private const string Guid = "com.dev.survivallog.windpower";
    private const string Version = "0.9.4";

    public override void Load()
    {
        _uninstallPrep = Config.Bind("Uninstall", "UninstallPrep", false,
            "Turn this on before you remove Wind Power. Once a save is loaded, every Wind Turbine Package becomes the Solar Panel Package of its size, in your bags, in storage and on the floor, in every place the save holds, and the shops stop selling the turbines. Placed and packed turbines are always saved as solar panels, so they need nothing. Then save the game, quit, and remove WindPower.dll. LogOutput.log lists what was changed. Turning this off again does not turn the packages back.\n" +
            "移除风力发电模组之前请先打开此项。读取存档后，背包、储物和地上的每个风力发电机包裹都会变成同尺寸的太阳能发电板包裹，存档里的每个地点都一样，商店也不再出售风力发电机。已安装和已打包的风力发电机本来就按太阳能发电板保存，不受影响。然后存档、退出游戏，再删除 WindPower.dll。改了哪些东西写在 LogOutput.log 里。再关掉此项，包裹也不会变回来。");

        var variation = Config.Bind("Wind", "Variation", WindVariation.DefaultPercent, new ConfigDescription(
            "How far the wind rises and falls within one weather, as a percent of that weather's average. The wind swells and slackens over the hours, the same for every turbine, and over a day it averages out to the weather's own. 0 keeps the wind steady for as long as the weather lasts.\n" +
            "同一种天气里风力上下起伏的幅度，按该天气平均风力的百分比计。风会随时间时强时弱，所有风力发电机一样，一天下来平均仍是该天气的风力。0 表示天气不变时风力也不变。",
            new AcceptableValueRange<int>(0, WindVariation.MostPercent)));

        LiveSettings.Watch(variation, percent => WindSettings.VariationPercent = percent);
        BindWeathers();
        _returnPercent = Config.Bind("Dismantle", "ReturnPercent", MaterialList.DefaultReturnPercent, new ConfigDescription(
            "How much of a turbine's materials taking it down gives back, as a percent of its recipe. 50 is about half, like the game's own furniture. 0 gives nothing back and 100 gives back everything. Each material's share is rounded down, then topped up one at a time to the right total. A turbine whose Returns list is filled in gives back that list instead.\n" +
            "拆除风力发电机时返还多少材料，按配方的百分比计。50 约为一半，和游戏自带的家具一样。0 什么都不返还，100 全部返还。每种材料按比例向下取整，再逐个补足到应有的总数。填了 Returns 清单的风力发电机按清单返还。",
            new AcceptableValueRange<int>(0, 100)));
        BindSettings(TurbineRows.Small, "Small Solar Panel", "小型太阳能发电板", 20);
        BindSettings(TurbineRows.Medium, "Medium Solar Panel", "中型太阳能发电板", 40);
        BindSettings(TurbineRows.Large, "Large Solar Panel", "大型太阳能发电板", 80);
        ModManagerAnswers.Settings = () => TurbineRows.All.Select(kind => _reads[kind]()).ToList();
        LiveSettings.Watch(_uninstallPrep, isOn =>
        {
            UninstallPrep.Set(isOn);
            if (isOn) Log.LogInfo("Uninstall prep is on: load a save to turn the turbine packages into solar panel packages");
        });

        var tables = new TurbineTables(message => Log.LogInfo(message));
        var weatherPage = new WeatherPage(message => Log.LogInfo(message));
        var harmony = new Harmony(Guid);
        var iconPlaced = false;

        WindPatches.Apply(harmony, message => Log.LogWarning(message));
        WindBoosts.OnFailed = source => Log.LogWarning($"{source}'s wind multiplier failed, so it no longer counts");
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
                TurbineIcon.Place();
            }

            TurbineIcon.Report(message => Log.LogInfo(message));

            tables.Tick(now);
            UninstallPrep.Tick(now);
            TurbineModel.Animate();
            weatherPage.Tick(now);
        };
        AddComponent<WindTicker>();
        Log.LogInfo($"Wind Power {Version} loaded");
    }

    /// <summary>Each weather's wind, one setting per weather in its own section. Every one applies live, within a second.</summary>
    private void BindWeathers()
    {
        for (var i = 0; i < WeatherWinds.All.Count; i++)
        {
            var index = i;
            var weather = WeatherWinds.All[i];
            var entry = Config.Bind("Weather", weather.Key, weather.DefaultPercent, new ConfigDescription(
                $"How hard the wind blows in {weather.Name}, as a percent of full wind: a turbine makes its RatedPower times this. 100 is full wind; Variation moves it up and down over the hours. 0 is still air, where the turbines stand still.\n" +
                $"{weather.Chinese}的风力，按满风力的百分比计：风力发电机的发电量为 RatedPower 乘以这个数。100 为满风力；「Variation」会让它随时间上下浮动。0 为无风，风力发电机不转。",
                new AcceptableValueRange<int>(0, WeatherWinds.MostPercent)));

            LiveSettings.Watch(entry, percent => WindSettings.SetWeather(index, percent));
        }
    }

    /// <summary>One section of settings per turbine. Every one applies live: the turbine's rows are rewritten when one changes.</summary>
    private void BindSettings(TurbineKind kind, string solar, string solarChinese, int solarPrice)
    {
        var section = kind.Section;
        var name = kind.Name;
        var chinese = kind.ChineseName;
        var defaults = kind.Defaults;
        var listTags = new[] { "ModManager.Control=ItemCounts", $"ModManager.MaxKinds={MaterialList.MostKinds}", $"ModManager.MaxCount={MaterialList.MostOfOne}" };

        var rated = Config.Bind(section, "RatedPower", defaults.RatedPower, new ConfigDescription(
            $"Power the {name} makes on average in the strongest wind. Calmer weather gives a share of it, the wind rises and falls around that (see Variation), and a turbine on the 2F terrace makes 20% more.\n" +
            $"{chinese}在风最大的天气里的平均发电量。风小的天气只发出其中一部分，风力会在此上下起伏（见 Variation），装在二楼露台的多发 20%。",
            new AcceptableValueRange<float>(1f, 200f)));
        var materials = Config.Bind(section, TurbineChoices.MaterialsKey, defaults.Materials, new ConfigDescription(
            $"What crafting a {name} Package takes, written as item id:count pairs split by commas. 20004 is Scrap Metal, 20104 Wire, 20210 Electronic Components and 20373 Stainless Steel Plate. Up to {MaterialList.MostKinds} different items, up to {MaterialList.MostOfOne} of each. A list the game cannot use (an unknown item, a dye, or the same materials as another recipe) falls back to the default, and LogOutput.log says why.\n" +
            $"制作{chinese}包裹所需的材料，写成「物品id:数量」，用逗号隔开。20004 是铁皮，20104 是铁丝，20210 是电器元件，20373 是不锈钢板。最多 {MaterialList.MostKinds} 种物品，每种最多 {MaterialList.MostOfOne} 个。游戏用不了的清单（物品不存在、含有染料，或和别的配方材料相同）会改用默认值，原因写在 LogOutput.log 里。",
            null, listTags.Append($"ModManager.Makes={kind.Ids.Item}").ToArray<object>()));
        var returns = Config.Bind(section, TurbineChoices.ReturnsKey, defaults.Returns, new ConfigDescription(
            $"What taking a {name} down gives back, written like Materials. Blank gives back the ReturnPercent share of its materials (under Dismantle); {MaterialList.NoItems} gives nothing back; a list gives back exactly that, and may name any items. Up to {MaterialList.MostKinds} different items, up to {MaterialList.MostOfOne} of each. A list the game cannot use (an unknown item) gives back the ReturnPercent share instead, and LogOutput.log says why.\n" +
            $"拆除{chinese}时返还的物品，写法和 Materials 一样。留空时按 Dismantle 下的 ReturnPercent 返还一部分材料；写 {MaterialList.NoItems} 什么都不返还；写清单就照清单返还，可以是任何物品。最多 {MaterialList.MostKinds} 种物品，每种最多 {MaterialList.MostOfOne} 个。游戏用不了的清单（物品不存在）会改按 ReturnPercent 返还，原因写在 LogOutput.log 里。",
            null, listTags.Concat(new[] { "ModManager.AutoWhenBlank", $"ModManager.ReturnsOf={TurbineChoices.MaterialsKey}" }).ToArray<object>()));
        var price = Config.Bind(section, "ShopPrice", defaults.ShopPrice, new ConfigDescription(
            $"What the Classic Furniture shop asks for a {name} before the disaster. The {solar} costs {solarPrice} there.\n" +
            $"灾难前「经典家具综合」商店卖{chinese}的价格。{solarChinese}在那里卖 {solarPrice}。",
            new AcceptableValueRange<int>(0, 9999)));
        var stock = Config.Bind(section, "ShopStock", defaults.ShopStock, new ConfigDescription(
            $"How many {name}s the Classic Furniture shop has for sale before the disaster. 0 takes them off the shelves. A shop that already has more than this is brought down to it; turbines already bought stay yours.\n" +
            $"灾难前「经典家具综合」商店有几台{chinese}出售。0 表示不卖。已有更多存货时会减到这个数；已经买下的不受影响。",
            new AcceptableValueRange<int>(0, 20)));

        // Uninstall prep holds the stock at 0, so the shops stop listing the turbine.
        TurbineSettings Read() => new(rated.Value, materials.Value, price.Value, _uninstallPrep!.Value ? 0 : stock.Value, _returnPercent!.Value, returns.Value);

        _reads[kind] = Read;
        LiveSettings.Watch(() => WindSettings.Set(kind, Read()), rated, materials, returns, price, stock, _uninstallPrep!, _returnPercent!);
    }

    private ConfigEntry<bool>? _uninstallPrep;
    private ConfigEntry<int>? _returnPercent;
    private readonly Dictionary<TurbineKind, Func<TurbineSettings>> _reads = new();
}
