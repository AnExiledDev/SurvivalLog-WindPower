# Wind Power

Adds three wind turbines to Survival Log. They make power day and night, more the windier the weather is, and can only be installed outdoors: in the yard outside or on the 2F terrace.

- **Small Wind Turbine**: three blades and a tail vane on a pole. Its head turns to face the wind. It fits a small furniture spot.
- **Medium Wind Turbine**: an upright rotor of two half drums that catches the wind from any side. It fits a medium furniture spot.
- **Large Wind Turbine**: three blades on a mast. Every so often a gust comes through, and the whole top swings round to meet it. It fits a large furniture spot.

Each turbine has its own model and icon. The rotor speeds up and slows down smoothly with the wind and stands still in Heavy Smog. Hover over a turbine to see, at its base, what it makes now and how strong the wind is. The weather box on the screen shows the wind too, as a Wind Intensity line.

## Getting them

- The Small and Medium Wind Turbines' recipes show up with the Medium Solar Panel's, when [Main Quest] Assemble Solar Panels begins. The Small Wind Turbine Package takes 3 Scrap Metal, 1 Wire and 1 Electronic Components; the Medium takes 4 Scrap Metal, 2 Wire and 2 Electronic Components.
- The Large Wind Turbine's recipe shows up with the Lead-Acid Battery Pack's, when [Main Quest] Store the Electricity begins. Its package takes 5 Scrap Metal, 3 Wire, 3 Electronic Components and 1 Stainless Steel Plate.
- A save that is already past those quests gets the recipes when it loads.
- Before the disaster, the Classic Furniture shop sells 2 of each: the Small Wind Turbine for 30, the Medium for 60 and the Large for 120.
- Taking a turbine down gives back about half of what its package took.
- **Test buttons**: with Cheat Menu installed, its Mod tests tab shows the wind now and what each turbine makes, or shows all the recipes right away.

## Power

A turbine makes less than the solar panel of its size, since it keeps going at night. You set each turbine's power, materials, shop price and shop stock yourself (see Settings). At the default power of 9, 18 and 36, a turbine in the yard makes:

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

A turbine on the 2F terrace stands higher in the wind and makes 20% more: up to 11 for the small, 22 for the medium and 43 for the large.

This is an early test build.

## Settings

Settings live in `BepInEx\config\com.dev.survivallog.windpower.cfg`, created the first time the game starts with the mod. Change them in game on the Mod Manager's Mods screen, or edit the file while the game is closed. See [CONFIG.md](CONFIG.md) for every setting. Without Exiled's Toolkit installed, changes made on the Mods screen take effect at the next game start instead.

## Save safety

The game saves each turbine as the solar panel of its size, and the mod turns it back into a turbine when the save loads. Without the mod, your turbines become solar panels and the save still loads.

## Uninstall

A turbine package in a bag or in storage would be lost without the mod, so do this first:

1. Turn on `UninstallPrep` (on the Mods screen, or in the config file while the game is closed).
2. Load your save. Every turbine package, wherever it is, becomes the solar panel package of its size, and the shops stop selling turbines. `BepInEx\LogOutput.log` says what was changed.
3. Save the game and quit.

Then delete `WindPower.dll` from the `BepInEx\plugins` folder. For a clean slate, also delete `com.dev.survivallog.windpower.cfg` from `BepInEx\config`. Do this for each save you want to keep.

## Requirements

- Survival Log 1.1.20151.
- BepInEx for Survival Log.
- Optional: [Exiled's Toolkit](https://github.com/AnExiledDev/SurvivalLog-ExiledToolkit) 0.4.0 or newer. With it, settings changed on the Mod Manager's Mods screen take effect without restarting the game. Without it, they take effect at the next game start.

## Install

Copy `WindPower.dll` into `BepInEx/plugins`.
