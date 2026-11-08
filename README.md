# Wind Power

Adds three wind turbines to Survival Log. They keep making power after dark, and the stronger the wind, the more they make. They only go outdoors: the Small in the yard, the Medium and Large in the yard or on the 2F terrace.

- **Small Wind Turbine**: three blades and a tail vane on a pole. Its head turns to face the wind. It fits a small furniture spot.
- **Medium Wind Turbine**: an upright rotor of two half drums that catches the wind from any side. It fits a medium furniture spot.
- **Large Wind Turbine**: three blades on a mast. Every so often a gust comes through, and the whole top swings round to meet it. It fits a large furniture spot.

Each turbine has its own model and icon, and can be dyed like the game's own furniture: the olive paint takes the colour, and the orange stripes and blades stay as they are. You can also craft one already coloured, by picking a dye on its recipe card at the workbench. The rotor speeds up and slows down smoothly with the wind and stands still in Heavy Smog. Hover over a turbine to see, at its base, what it makes now and how strong the wind is. The wind rises and falls over the hours, and the rotors follow it. The weather box on the screen shows the wind too, as a Wind line. The wind reads as a word and its percent of full wind, such as Moderate (35%/100%): Calm in Heavy Smog, Light on clear and cloudy days, Moderate in rain, Strong in the cold snaps and Heavy Rain, and Gale in Severe Cold. A gust can take it past 100%, up to Gale (130%/100%) at the default Variation.

## Getting them

- The Small and Medium Wind Turbines' recipes show up with the Medium Solar Panel's, when [Main Quest] Assemble Solar Panels begins. The Small Wind Turbine Package takes 3 Scrap Metal, 1 Wire and 1 Electronic Components; the Medium takes 4 Scrap Metal, 2 Wire and 2 Electronic Components.
- The Large Wind Turbine's recipe shows up with the Lead-Acid Battery Pack's, when [Main Quest] Store the Electricity begins. Its package takes 5 Scrap Metal, 3 Wire, 3 Electronic Components and 1 Stainless Steel Plate.
- A save that is already past those quests gets the recipes when it loads.
- Before the disaster, the Classic Furniture shop sells 2 of each: the Small Wind Turbine for 30, the Medium for 60 and the Large for 120.
- Taking a turbine down gives back about half of what its package took. The `ReturnPercent` setting changes how much, and each turbine's `Returns` setting can name exactly what it gives back instead.
- On the Mod Manager's Mods screen (0.14.0 or newer), each turbine's materials and what it gives back show as item cards you pick from every item in the game. The screen shows what the recipe is worth, warns when taking the turbine down would give back more than that, and says when the turbine cannot use a list, such as one with exactly the same materials as another recipe.
- **Test buttons**: with Cheat Menu installed, its Mod tests tab shows the wind now and what each turbine makes, the wind for the next 12 hours, and what each turbine takes and gives back. It can also show all the recipes right away, and put one of each turbine package, the materials for each, or one of each dye in your bag.

## Power

A turbine makes less than the solar panel of its size, since it keeps going at night. You set each turbine's power, materials, shop price and shop stock yourself, and how hard the wind blows in each weather (see Settings). At the default power of 9, 18 and 36, a turbine in the yard makes:

| Weather | Small | Medium | Large |
| --- | --- | --- | --- |
| Heavy Smog | 0 | 0 | 0 |
| Clear, and the cold clear days | 1 | 2 | 4 |
| Cloudy | 2 | 4 | 7 |
| Rainy | 3 | 6 | 13 |
| Continuous Rain | 4 | 8 | 16 |
| Mild Cold | 5 | 10 | 20 |
| Heavy Rain | 7 | 14 | 27 |
| Moderate Cold | 7 | 14 | 29 |
| Severe Cold | 9 | 18 | 36 |

A Medium or Large turbine on the 2F terrace stands higher in the wind and makes 20% more: up to 22 for the medium and 43 for the large. The terrace has no small spots.

A turbine's power setting is what it makes in full wind, the average wind in Severe Cold. The Wind line in the weather box shows how hard the wind blows compared to that, such as Moderate (35%/100%), and a turbine makes its power setting times that percent: a Large set to 36 makes about 13 at 35%. 100% isn't the top, since a gust can push it past 100%. What each word means, and the weather you'll usually see it in:

- **Calm** (0%): Heavy Smog. The turbines stand still.
- **Light** (1-29%): clear and cloudy days.
- **Moderate** (30-49%): Rainy and Continuous Rain.
- **Strong** (50-89%): Mild Cold, Moderate Cold and Heavy Rain.
- **Gale** (90% and up): Severe Cold.

These are averages. Within a weather the wind rises and falls over the hours, by up to 30% either way at the default, so a rainy afternoon might give a small turbine 2 one hour and 4 the next. Every turbine feels the same wind, and over several days it evens out to the numbers above. The `Variation` setting widens or narrows the swing, and 0 keeps the wind steady.

With [Power Manager](https://www.nexusmods.com/survivallog/mods/126) 0.4.0 or newer, its `WindMultiplier` setting makes every turbine stronger or weaker, like its solar setting does for the panels. A turbine's hover then shows its power with that setting, and the wind reading stays as it is. With Power Manager 0.5.0 or newer, its `SmogOutput` setting can also give Heavy Smog some wind, as a share of a Clear day's, so the turbines turn and make power there too.

## Settings

Settings live in `BepInEx\config\com.dev.survivallog.windpower.cfg`, created the first time the game starts with the mod. Change them in game on the Mod Manager's Mods screen, or edit the file while the game is closed. See [CONFIG.md](CONFIG.md) for every setting. Every setting takes effect at once, without restarting the game.

## Save safety

The game saves each turbine as the solar panel of its size, and the mod turns it back into a turbine when the save loads. Without the mod, your turbines become solar panels and the save still loads.

## Uninstall

A turbine package in a bag or in storage would be lost without the mod, so do this first:

1. Turn on `UninstallPrep` (on the Mods screen, or in the config file while the game is closed).
2. Load your save. Every turbine package, wherever it is, becomes the solar panel package of its size and colour, and the shops stop selling turbines. `BepInEx\LogOutput.log` says what was changed.
3. Save the game and quit.

Then delete `WindPower.dll` from the `BepInEx\plugins` folder. For a clean slate, also delete `com.dev.survivallog.windpower.cfg` from `BepInEx\config`. Do this for each save you want to keep.

## Requirements

- Survival Log 1.1.20719.
- BepInEx for Survival Log.
- [Exiled's Toolkit](https://github.com/AnExiledDev/SurvivalLog-ExiledToolkit) 0.19.0 or newer. Wind Power does not load without it. It lets settings changed on the Mod Manager's Mods screen take effect without restarting the game.
- Optional: [Mod Manager](https://www.nexusmods.com/survivallog/mods/73). With it, you can change every setting in game on its Mods screen and see what each one does, without opening the config file. Each turbine's materials and what it gives back when taken down show as item cards with the item's picture and name: pick an item from a list and set how many, with no item ids to look up.

## Install

1. Install the [BepInEx Pack for Survival Log](https://www.nexusmods.com/survivallog/mods/12) (if no other mods were installed before, start the game once so BepInEx finishes setup, then quit).
2. Install Exiled's Toolkit 0.19.0 or newer. Download `ExiledToolkit-x.y.z.zip` from [its latest release](https://github.com/AnExiledDev/SurvivalLog-ExiledToolkit/releases/latest) or from [Nexus Mods](https://www.nexusmods.com/survivallog/mods/61), and extract it into the game folder (the folder with `SurvivalLog.exe`).
3. Download `WindPower-x.y.z.zip` from the [latest release](https://github.com/AnExiledDev/SurvivalLog-WindPower/releases/latest) or from [Nexus Mods](https://www.nexusmods.com/survivallog/mods/136).
4. Extract the zip into the game folder. It holds a `BepInEx` folder, so the DLL lands in the right place. When asked, merge the folders. The result should be:
   `C:\Program Files (x86)\Steam\steamapps\common\Survival Log\BepInEx\plugins\WindPower.dll`

## Known issues

- Uninstall prep works on the save you load while it is on, so each save you want to keep needs its own load and save before you remove the mod.

## Feedback and bug reports

Found a bug, have an idea, or something feels off? Come say so on the Core Directive Discord: https://discord.gg/trTsyxUFqg . You can also post in the Posts tab of the mod's Nexus Mods page. When reporting a bug, attach `BepInEx\LogOutput.log`.

To reach me directly, message `exiled.dev` on Discord, you can find me in the [official Survival Log Discord](https://discord.gg/MZGg4gQAUe). I'm also on X as [@AnExiledDev](https://x.com/AnExiledDev).

A Survival Log wiki with detailed documentation for all of my mods is coming soon.

## License

MIT. Copyright (c) 2026 AnExiledDev. See [LICENSE](LICENSE).
