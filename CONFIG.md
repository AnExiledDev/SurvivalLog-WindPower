# Wind Power settings

Settings live in `BepInEx\config\com.dev.survivallog.windpower.cfg`, created the first time the game starts with the mod. Change them in game on the Mod Manager's Mods screen, where they take effect at once, or edit the file while the game is closed. Delete the file to get the defaults back. Without Exiled's Toolkit installed, changes made on the Mods screen take effect at the next game start instead.

## MediumTurbine and LargeTurbine

Each turbine has its own section, `[MediumTurbine]` and `[LargeTurbine]`, with the same four settings.

| Setting | Medium default | Large default | What it does |
| --- | --- | --- | --- |
| `RatedPower` | `18` | `36` | The power the turbine makes in Severe Cold, the windiest weather. Calmer weather gives a share of it: none in Heavy Smog, about a third in rain. A turbine on the 2F terrace makes 20% more. Any value from `1` to `200`. |
| `Materials` | `20004:4, 20104:2, 20210:2` | `20004:5, 20104:3, 20210:3, 20373:1` | What crafting the turbine's package takes, written as item id:count pairs split by commas. 20004 is Scrap Metal, 20104 Wire, 20210 Electronic Components and 20373 Stainless Steel Plate. Up to 7 different items and up to 99 of each. A list the game cannot use (one it cannot read, an item that does not exist, or the same materials as another recipe, the other turbine's included) falls back to the default, with a line in `BepInEx\LogOutput.log` saying why. |
| `ShopPrice` | `60` | `120` | What the Classic Furniture shop asks for the turbine before the disaster. The Medium Solar Panel costs 40 there and the Large Solar Panel 80. Any value from `0` to `9999`. |
| `ShopStock` | `2` | `2` | How many of the turbine the Classic Furniture shop has for sale before the disaster. `0` takes them off the shelves. A shop that already has more than this is brought down to it, and turbines you already bought stay yours. Any value from `0` to `20`. |
