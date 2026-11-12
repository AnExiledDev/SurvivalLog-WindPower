# Changelog

## 0.9.6

- Clearer wording in the settings and on screen.
- Made for Survival Log 1.1.20719.

## 0.9.5

- Now needs Exiled's Toolkit 0.20.0 or newer.
- If a game update stops part of the mod from working, the Mod Manager now shows that Wind Power may need an update.
- Made for Survival Log 1.1.20719.

## 0.9.4

- New Weather settings set how hard the wind blows in each weather, as a percent of full wind. The defaults keep the wind as it was.
- Power Manager 0.5.0 or newer can now give Heavy Smog some wind with its SmogOutput setting, so the turbines turn and make power there. Without it, Heavy Smog stays still as before.
- Made for Survival Log 1.1.20719.

## 0.9.3

- Power Manager 0.4.0 or newer can now make the turbines stronger or weaker with its WindMultiplier setting. Without it, nothing changes.
- Made for Survival Log 1.1.20719.

## 0.9.2

- The wind now shows as a word and a percent of full wind, such as Light (20%/100%). The words are Calm, Light, Moderate, Strong and Gale.
- 100% is full wind, and a gust can go past it, so Severe Cold can show Gale (130%/100%).
- The line in the weather box is now called Wind, and a turbine's hover shows the same reading.
- Made for Survival Log 1.1.20719.

## 0.9.1

- Fixed: with UninstallPrep on, a shop could get its turbine entries back when it loaded. Now a save made with UninstallPrep on has no turbine entries left in any shop.
- Shops that never sell a turbine no longer get turbine entries added to them.
- If setting up the turbines fails partway through, the mod now tries again a few seconds later instead of leaving them half set up.
- Made for Survival Log 1.1.20719.

## 0.9.0

- The turbines can be crafted in any of the game's 8 colours, like the solar panels: pick a dye on the turbine's recipe card at the workbench. The coloured package has its own icon and unpacks into a dyed turbine.
- A dye can no longer be one of a turbine's Materials, since picking a colour adds one.
- Raising a turbine's ShopStock now also reaches a shop that wasn't loaded when you changed it, and the turbines you already bought there still count. Before, only a shop that was loaded at the time got the raise.
- With Cheat Menu installed, its Mod tests tab has a Give 1 of each dye button.
- Made for Survival Log 1.1.20719.

## 0.8.2

- With Cheat Menu installed, its Mod tests tab has four more Wind Power buttons: Give 1 of each turbine, Give materials for each turbine, Show recipes and returns, and Show the wind for 12 hours.
- Made for Survival Log 1.1.20719.

## 0.8.1

- Now uses Exiled's Toolkit for its shared parts, so it needs Exiled's Toolkit 0.19.0 or newer.
- Made for Survival Log 1.1.20719.

## 0.8.0

- New setting for each turbine: Returns. It sets exactly what taking the turbine down gives back, and it can be any items you like. Left blank, the turbine gives back its ReturnPercent share as before. Set to none, it gives back nothing.
- With Mod Manager 0.14.0 or newer, each turbine's Materials and Returns show as item cards you pick from every item in the game, with what the recipe is worth and a warning when taking the turbine down would give back more than that.
- The Mods screen now says why a turbine cannot use a materials list, such as one with exactly the same materials as one of the game's recipes or another turbine's.
- Made for Survival Log 1.1.20151.

## 0.7.1

- New setting: ReturnPercent, under Dismantle. It sets how much of a turbine's materials taking it down gives back, from 0 to 100% of its recipe. The default of 50 gives back the same as before.
- Made for Survival Log 1.1.20151.

## 0.7.0

- The wind now rises and falls within a weather instead of blowing at one strength all day. It picks up and dies down over the hours, by up to 30% either way at the default setting, and every turbine and its rotor follow it. Over a few days it evens out to the same power as before.
- New setting: Variation, under Wind. It sets how far the wind swings, and 0 keeps it steady like before.
- Made for Survival Log 1.1.20151.

## 0.6.2

- The Small and Large Wind Turbines stand in the middle of their spot. Before, their base stuck out over one edge.
- Made for Survival Log 1.1.20151.

## 0.6.1

- Dyed turbines now take the colour the same way the game's furniture does, so they match a dyed solar panel next to them.
- Made for Survival Log 1.1.20151.

## 0.6.0

- The turbines can be dyed, like the game's furniture, in any of its 8 colours. The olive paint takes the colour, and the orange stripes, the blades and the rust stay as they are.
- Made for Survival Log 1.1.20151.

## 0.5.0

- New: a Small Wind Turbine. Three blades and a tail vane on a pole, for a small furniture spot in the yard outside. Its head turns to face the wind.
- The Small Wind Turbine makes up to 9 power. Its recipe shows up with the Medium Solar Panel's, when [Main Quest] Assemble Solar Panels begins, and its package takes 3 Scrap Metal, 1 Wire and 1 Electronic Components. The Classic Furniture shop sells 2 before the disaster, for 30 each.
- Taking a turbine down now gives back about half of what its package took, following its Materials setting. Before, it gave back the solar panel's parts.
- New setting: UninstallPrep, under Uninstall. Turn it on before you remove the mod and load your save: turbine packages in bags, in storage and on the floor become solar panel packages of the same size, and the shops stop selling turbines. Then save, quit and remove WindPower.dll, and nothing is lost.
- The Cheat Menu button Show both recipes now is renamed to Show all recipes now.
- Made for Survival Log 1.1.20151.

## 0.4.1

- With Cheat Menu installed, its Mod tests tab has two Wind Power buttons: Show turbines (the wind now and what each turbine makes) and Show both recipes now.

## 0.4.0

- New: a Medium Wind Turbine. Its upright rotor of two half drums catches the wind from any side, and it fits a medium furniture spot in the yard outside or on the 2F terrace.
- The Medium Wind Turbine makes up to 18 power, and the Large Wind Turbine now makes up to 36 instead of 48. A turbine runs day and night, so at 48 it was far better than a solar panel.
- The Medium Wind Turbine's recipe shows up with the Medium Solar Panel's, when [Main Quest] Assemble Solar Panels begins. Its package takes 4 Scrap Metal, 2 Wire and 2 Electronic Components.
- The Large Wind Turbine's recipe now shows up with the Lead-Acid Battery Pack's, when [Main Quest] Store the Electricity begins. Its package now takes 5 Scrap Metal, 3 Wire, 3 Electronic Components and 1 Stainless Steel Plate.
- A save that is already past those quests gets the turbine recipes when it loads.
- The text of both quests now mentions the wind turbine.
- Before the disaster, the Classic Furniture shop sells both turbines.
- New settings for each turbine: the power it makes, what its package takes, its price in the shop and how many the shop has. With Exiled's Toolkit installed, a change takes effect without restarting the game.
- Made for Survival Log 1.1.20151.

## 0.3.1

- The turbine's power now follows the weather much more closely: nothing in Heavy Smog, 6 on a Clear day, 17 in rain, 36 in Heavy Rain and up to 48 in Severe Cold.
- A turbine on the 2F terrace makes a fifth more power than one in the yard, because it stands higher in the wind.
- The blades now turn faster, speed up and slow down smoothly with the wind, and stand still in Heavy Smog.
- Every so often a gust comes through: the whole top of the turbine swings round to meet it and the blades speed up, then both settle back.
- The power shown when you hover over the turbine now appears at its base, so you can see it when zoomed in.
- The weather box on the screen now shows a Wind Intensity line under Light Intensity and Cold Intensity.
- Made for Survival Log 1.1.20151.

## 0.2.0

- The Large Wind Turbine now has its own model, with blades that turn faster in stronger wind.
- The turbine package has its own icon in the crafting menu and your bag.
- The turbine now makes power from the wind, day and night: from 6 in still air up to 42 in Severe Cold.
- Hovering over the turbine shows the power it makes now and how strong the wind is.
- Fixed: the turbine's recipe did not show in the crafting menu.
- The turbine now needs its own materials: 6 Scrap Metal, 4 Electrical Wire, 4 Stainless Steel Plate and 3 Electronic Components.
- Removing the mod no longer breaks a save: installed turbines turn into Large Solar Panels instead.
- Made for Survival Log 1.1.20151.

## 0.1.0

- New: a craftable Large Wind Turbine that can only be installed outdoors, in the yard outside or on the 2F terrace.
- For now it looks like the Large Solar Panel and makes power the same way.
- Made for Survival Log 1.1.20151.
