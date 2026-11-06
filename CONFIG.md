# Wind Power settings

Settings live in `BepInEx\config\com.dev.survivallog.windpower.cfg`, created the first time the game starts with the mod. Change them in game on the Mod Manager's Mods screen, where they take effect at once, or edit the file while the game is closed. Delete the file to get the defaults back.

## SmallTurbine, MediumTurbine and LargeTurbine

Each turbine has its own section, `[SmallTurbine]`, `[MediumTurbine]` and `[LargeTurbine]`, with the same five settings. On the Mod Manager's Mods screen (0.14.0 or newer), `Materials` and `Returns` show as item cards you pick from every item in the game, and the screen says when the turbine cannot use a list.

| Setting | Small default | Medium default | Large default | What it does |
| --- | --- | --- | --- | --- |
| `RatedPower` | `9` | `18` | `36` | The power the turbine makes on average in Severe Cold, the windiest weather, before `Variation` moves it up and down. Calmer weather gives a share of it: none in Heavy Smog, about a third in rain. A turbine on the 2F terrace makes 20% more. Any value from `1` to `200`. |
| `Materials` | `20004:3, 20104:1, 20210:1` | `20004:4, 20104:2, 20210:2` | `20004:5, 20104:3, 20210:3, 20373:1` | What crafting the turbine's package takes, written as item id:count pairs split by commas. 20004 is Scrap Metal, 20104 Wire, 20210 Electronic Components and 20373 Stainless Steel Plate. Up to 7 different items and up to 99 of each. A list the game cannot use (one it cannot read, an item that does not exist, a dye, or the same materials as another recipe, another turbine's included) falls back to the default, with a line in `BepInEx\LogOutput.log` saying why. Taking the turbine down gives back a share of these (see `Returns` and `ReturnPercent`). |
| `Returns` | blank | blank | blank | What taking the turbine down gives back, written like `Materials`. Blank gives back the `ReturnPercent` share of its materials. `none` gives nothing back. A list gives back exactly that, and may name any items, not only the recipe's. Up to 7 different items and up to 99 of each. A list the game cannot use (one it cannot read, or an item that does not exist) gives back the `ReturnPercent` share instead, with a line in `BepInEx\LogOutput.log` saying why. |
| `ShopPrice` | `30` | `60` | `120` | What the Classic Furniture shop asks for the turbine before the disaster. The Small Solar Panel costs 20 there, the Medium 40 and the Large 80. Any value from `0` to `9999`. |
| `ShopStock` | `2` | `2` | `2` | How many of the turbine the Classic Furniture shop has for sale before the disaster. `0` takes them off the shelves. A shop that already has more than this is brought down to it, and turbines you already bought stay yours. Any value from `0` to `20`. |

## Wind

| Setting | Default | What it does |
| --- | --- | --- |
| `Variation` | `30` | How far the wind rises and falls within one weather, as a percent of that weather's average. The wind swells and slackens over the hours, the same for every turbine, and over a day it averages out to the weather's own. `0` keeps the wind steady for as long as the weather lasts. Any value from `0` to `60`. |

## Dismantle

| Setting | Default | What it does |
| --- | --- | --- |
| `ReturnPercent` | `50` | How much of a turbine's materials taking it down gives back, as a percent of its recipe. `50` is about half, like the game's own furniture: the Large Wind Turbine gives back 3 Scrap Metal, 2 Wire and 1 Electronic Components. `0` gives nothing back and `100` gives back everything. Each material's share is rounded down, then topped up one at a time to the right total. A turbine whose `Returns` list is filled in gives back that list instead. Any value from `0` to `100`. |

## Uninstall

| Setting | Default | What it does |
| --- | --- | --- |
| `UninstallPrep` | `false` | Turn this on before you remove the mod, then load your save. Every turbine package, in bags, in storage, on the floor and in every place the save holds, becomes the solar panel package of its size, and the shops stop selling turbines. Placed and packed turbines are always saved as solar panels, so they need nothing. Save the game, quit, and remove `WindPower.dll`. `BepInEx\LogOutput.log` says what was changed. Turning it off again does not turn the packages back. |
