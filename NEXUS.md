Name: Wind Power
Summary: Adds Small, Medium and Large Wind Turbines with animated models, icons and recipes. Installed outdoors, they make power day and night from the wind, more on the 2F terrace. Recipes come with the solar panel and battery quests, the Classic Furniture shop sells all three, power, materials and prices are settings, and one setting makes removal safe.

Description (BBCode):
[center][img]https://sldb.core-directive.com/media/save/3-remove-with-care.png[/img][/center]

Wind Power adds a Small, a Medium and a Large Wind Turbine to Survival Log.

[size=5]Main features[/size]
[list]
[*][b]Three turbines[/b]: the Small Wind Turbine has three blades and a tail vane on a pole, and its head turns to face the wind. The Medium Wind Turbine has an upright rotor of two half drums that catches the wind from any side. The Large Wind Turbine has three blades on a mast, and every so often a gust swings its whole top round and speeds the blades up. Each has its own model and icon.[/*]
[*][b]Power from the wind[/b]: they make power day and night, more the windier the weather is, and less than the solar panel of the same size. Nothing in Heavy Smog, then on average 9 for the small, 18 for the medium and 36 for the large in Severe Cold. All three numbers are settings.[/*]
[*][b]Higher is windier[/b]: a Medium or Large turbine on the 2F terrace makes 20% more than one in the yard.[/*]
[*][b]The wind comes and goes[/b]: within a weather the wind rises and falls over the hours, and the turbines and their rotors follow it. Over several days it evens out. A setting sets how far it swings.[/*]
[*][b]Part of the story[/b]: the Small and Medium Wind Turbines' recipes show up with the Medium Solar Panel's, at [Main Quest] Assemble Solar Panels. The Large Wind Turbine's comes with the Lead-Acid Battery Pack's, at [Main Quest] Store the Electricity. Both quests mention the turbines, and a save already past them gets the recipes when it loads.[/*]
[*][b]Craft it[/b]: the Small Wind Turbine Package takes 3 Scrap Metal, 1 Wire and 1 Electronic Components. The Medium takes 4 Scrap Metal, 2 Wire and 2 Electronic Components. The Large takes 5 Scrap Metal, 3 Wire, 3 Electronic Components and 1 Stainless Steel Plate. Taking one down gives back about half.[/*]
[*][b]Or buy it[/b]: before the disaster, the Classic Furniture shop sells 2 of each, the small for 30, the medium for 60 and the large for 120.[/*]
[*][b]Your balance[/b]: each turbine's power, materials, what taking it down gives back, shop price and shop stock are settings on the Mod Manager's Mods screen. With Mod Manager 0.14.0 or newer, materials are item cards you pick from every item in the game, and the screen warns when a turbine would give back more than its recipe is worth.[/*]
[*][b]See the wind[/b]: the weather box on the screen shows a Wind Intensity line, and hovering over a turbine shows its output and the wind at its base.[/*]
[*][b]Outdoors only[/b]: each turbine fits the furniture spot of its size: the Small in the yard outside, the Medium and Large in the yard or on the 2F terrace. None go indoors.[/*]
[*][b]Dye them[/b]: the turbines can be dyed like the game's own furniture, in any of its 8 colours, or crafted already coloured by picking a dye on the recipe card. The olive paint takes the colour; the orange stripes and blades stay as they are.[/*]
[*][b]Safe to remove[/b]: the game saves each turbine as the solar panel of its size, so a save still loads without the mod. Before you remove it, turn on the UninstallPrep setting and load your save: turbine packages in bags and storage become solar panel packages, so nothing is lost. Then save, quit and delete WindPower.dll.[/*]
[*][b]English and Chinese[/b]: every name and description comes in both.[/*]
[/list]

[size=5]Known issues[/size]
Uninstall prep works on the save you load while it is on, so each save you want to keep needs its own load and save before you remove the mod.

[center][img]https://sldb.core-directive.com/img/mods-banner.jpg[/img]
[size=4][url=https://www.nexusmods.com/survivallog/mods/73]Get Mod Manager[/url] to set up all your mods in game  |  [url=https://sldb.core-directive.com/en/]Open the Survival Log wiki[/url] for items, recipes and maps[/size][/center]

[size=5]Requirements[/size]
[list]
[*]Survival Log 1.1.20719. A game update can break it until the mod is updated.[/*]
[*][url=https://www.nexusmods.com/survivallog/mods/12]BepInEx for Survival Log[/url]. Install it first.[/*]
[*][url=https://www.nexusmods.com/survivallog/mods/61]Exiled's Toolkit[/url] 0.19.0 or newer. Wind Power does not load without it. It lets settings changed on the Mod Manager's Mods screen take effect without restarting the game.[/*]
[/list]

[size=5]Installation[/size]
[list=1]
[*]Install [url=https://www.nexusmods.com/survivallog/mods/12]BepInEx[/url] [i](if no other mods were installed before, start the game once so BepInEx finishes setup, then quit)[/i].[/*]
[*]Install [url=https://www.nexusmods.com/survivallog/mods/61]Exiled's Toolkit[/url] the same way: download its zip and extract it into the game folder.[/*]
[*]Download the zip from the Files tab.[/*]
[*]Extract it into the game folder (the folder with [font=Courier New]SurvivalLog.exe[/font]). The zip holds a BepInEx folder, so the DLL lands in the right place. When asked, merge the folders. The result should be:
[code]C:\Program Files (x86)\Steam\steamapps\common\Survival Log\BepInEx\plugins\WindPower.dll[/code][/*]
[/list]

[size=5]Uninstall[/size]
A turbine package in a bag or in storage would be lost without the mod, so first turn on [font=Courier New]UninstallPrep[/font] and load your save: every turbine package becomes the solar panel package of its size and colour. Save and quit, then delete [font=Courier New]WindPower.dll[/font] from the [font=Courier New]BepInEx\plugins[/font] folder. Do this for each save you want to keep.

[size=5]Configuration[/size]
To change settings, edit [font=Courier New]BepInEx\config\com.dev.survivallog.windpower.cfg[/font] while the game is closed, or do it in game on the Mod Manager's Mods screen (see [url=https://github.com/AnExiledDev/SurvivalLog-WindPower/blob/main/CONFIG.md]CONFIG.md[/url] for every setting). Every setting takes effect at once.

[size=5]Feedback[/size]
Found a bug or have an idea? Come say hi on the [url=https://discord.gg/trTsyxUFqg]Core Directive Discord[/url], or post in this page's Posts tab. For bugs, please include [font=Courier New]BepInEx\LogOutput.log[/font].
To reach me directly, message [b]exiled.dev[/b] on Discord; you can find me in the [url=https://discord.gg/MZGg4gQAUe]official Survival Log Discord[/url]. I'm also on X as [url=https://x.com/AnExiledDev]@AnExiledDev[/url].

[size=5]Source and license[/size]
Open source under MIT: [url=https://github.com/AnExiledDev/SurvivalLog-WindPower]github.com/AnExiledDev/SurvivalLog-WindPower[/url]

[line]
[size=5]简体中文[/size]

风力发电为《Survival Log》新增小型、中型和大型三种风力发电机。

[size=5]主要功能[/size]
[list]
[*][b]三种风力发电机[/b]：小型风力发电机是杆子上的三片叶片加一个尾翼，机头会转向迎风。中型风力发电机是一根竖轴上装着两片半圆筒，四面来风都能接住。大型风力发电机是桅杆上的三片叶片，时不时会有一阵阵风吹来，整个机头会随之转动，叶片也会转得更快。每种都有自己的模型和图标。[/*]
[*][b]靠风发电[/b]：白天黑夜都能发电，风越大发电越多，但比同尺寸的太阳能板少。「大雾霾天」不发电，「重度寒潮」中小型平均 9，中型平均 18，大型平均 36。这些数字都可以在设置里改。[/*]
[*][b]越高风越大[/b]：装在二楼露台的中型和大型风力发电机比屋外空地多发 20% 的电。[/*]
[*][b]风有强有弱[/b]：同一种天气里，风会随时间时强时弱，风力发电机的发电量和转速也跟着变。几天下来会平均回来。起伏幅度可以在设置里调。[/*]
[*][b]融入剧情[/b]：小型和中型风力发电机的配方和中型太阳能板包裹的一起出现，在主线任务「拼装太阳能板」开始时。大型风力发电机的配方和铅酸蓄电池包裹的一起出现，在主线任务「把电存下来」开始时。两个任务的说明都会提到风力发电机，已经做过这两个任务的存档，读档时就会得到配方。[/*]
[*][b]可以制作[/b]：小型风力发电机包裹需要 3 个铁皮、1 个铁丝和 1 个电器元件。中型需要 4 个铁皮、2 个铁丝和 2 个电器元件。大型需要 5 个铁皮、3 个铁丝、3 个电器元件和 1 个不锈钢板。拆除时返还大约一半材料。[/*]
[*][b]也可以买[/b]：灾难前，「经典家具综合」商店三种各卖 2 台，小型 30，中型 60，大型 120。[/*]
[*][b]自己调平衡[/b]：每台风力发电机的发电量、材料、拆除返还的物品、商店价格和库存，都可以在 Mod Manager 的「模组」界面里设置。用 Mod Manager 0.14.0 或更新版本时，材料以物品卡片显示，可以从游戏全部物品里挑选；拆除返还的价值高于配方时，界面会提醒你。[/*]
[*][b]看得到风[/b]：屏幕上的天气框会多一行「风力强度」，鼠标悬停在风力发电机上，会在它的底座旁显示当前发电量和风力。[/*]
[*][b]只能装在户外[/b]：每种都需要同尺寸的家具位：小型只能装在屋外空地，中型和大型可装在屋外空地或二楼露台，室内都无法安装。[/*]
[*][b]可以染色[/b]：风力发电机和游戏自带的家具一样可以染色，8 种颜色都能用，也可以在配方卡上选染料直接做出彩色的。橄榄绿的漆会变成所选颜色，橙色条纹和叶片保持原样。[/*]
[*][b]可以放心移除[/b]：游戏会把每台风力发电机存成同尺寸的太阳能板，所以移除本模组后存档仍能读取。移除前请先打开 UninstallPrep 设置并读取存档：背包和储物里的风力发电机包裹会变成太阳能发电板包裹，不会丢失。然后存档、退出，再删除 WindPower.dll。[/*]
[*][b]中英双语[/b]：所有名称和说明都有中文和英文。[/*]
[/list]

[size=5]已知问题[/size]
「UninstallPrep」只处理开启时读取的那个存档，所以每个想保留的存档，在移除模组前都要各自读取一次并保存。

[url=https://www.nexusmods.com/survivallog/mods/73]获取 Mod Manager[/url]，在游戏里设置所有模组。[url=https://sldb.core-directive.com/zh/]打开 Survival Log 维基[/url]，物品、配方、地图一查就有。

[size=5]前置需求[/size]
[list]
[*]Survival Log 1.1.20719。游戏更新后，在模组更新前可能无法使用。[/*]
[*][url=https://www.nexusmods.com/survivallog/mods/12]BepInEx for Survival Log[/url]，请先安装。[/*]
[*][url=https://www.nexusmods.com/survivallog/mods/61]Exiled's Toolkit[/url] 0.19.0 或更新版本。没有它本模组不会加载。它让在 Mod Manager 的「模组」界面改动的设置无需重启游戏即可生效。[/*]
[/list]

[size=5]安装[/size]
[list=1]
[*]安装 [url=https://www.nexusmods.com/survivallog/mods/12]BepInEx[/url] [i]（如果之前没有装过其他模组，先启动一次游戏，让 BepInEx 完成初始化，然后退出）[/i]。[/*]
[*]用同样的方式安装 [url=https://www.nexusmods.com/survivallog/mods/61]Exiled's Toolkit[/url]：下载它的压缩包并解压到游戏目录。[/*]
[*]在「文件」页下载压缩包。[/*]
[*]把压缩包解压到游戏目录（[font=Courier New]SurvivalLog.exe[/font] 所在的文件夹）。压缩包里带有 BepInEx 文件夹，DLL 会自动放到正确位置。如果提示合并文件夹，选择合并。解压后应为：
[code]C:\Program Files (x86)\Steam\steamapps\common\Survival Log\BepInEx\plugins\WindPower.dll[/code][/*]
[/list]

[size=5]卸载[/size]
没有本模组时，背包和储物里的风力发电机包裹会丢失，所以请先打开 [font=Courier New]UninstallPrep[/font] 并读取存档：所有风力发电机包裹都会变成同尺寸、同颜色的太阳能发电板包裹。存档并退出，再从 [font=Courier New]BepInEx\plugins[/font] 文件夹中删除 [font=Courier New]WindPower.dll[/font]。每个想保留的存档都要这样做一次。

[size=5]配置[/size]
可以在游戏内 Mod Manager 的「模组」界面修改，也可以在游戏关闭时编辑 [font=Courier New]BepInEx\config\com.dev.survivallog.windpower.cfg[/font]。配置文件中每项设置都有中文说明，完整说明见 [url=https://github.com/AnExiledDev/SurvivalLog-WindPower/blob/main/CONFIG.md]CONFIG.md[/url]（英文）。所有设置都即时生效。

[size=5]反馈[/size]
发现 bug 或有好点子？欢迎来 [url=https://discord.gg/trTsyxUFqg]Core Directive Discord[/url] 打个招呼，或在本页的「Posts」标签里发帖。报告 bug 时，请附上 [font=Courier New]BepInEx\LogOutput.log[/font]。
想直接联系我，可以在 Discord 上给 [b]exiled.dev[/b] 发消息，我在 [url=https://discord.gg/MZGg4gQAUe]Survival Log 官方 Discord[/url] 里。也可以在 X 上找我：[url=https://x.com/AnExiledDev]@AnExiledDev[/url]。

[size=5]源代码与许可[/size]
基于 MIT 许可开源：[url=https://github.com/AnExiledDev/SurvivalLog-WindPower]github.com/AnExiledDev/SurvivalLog-WindPower[/url]
