# Warehouse Helper

A MelonLoader mod for **Probably Stolen Demo**. Turn salvaged parts into a warehouse helper and assign repetitive item actions to hotkeys.

Version **0.2.0**, compiled against **game 0.46D / IL2CPP / Windows x64**, **MelonLoader 0.7.3**, and **.NET 6**. Compatibility with other game versions is unconfirmed. Compilation has passed; in-game testing has been performed by the player.

[中文](README.md) · [Changelog](CHANGELOG.md) · [Releases](https://github.com/githubnano/probably-stolen-warehouse-helper/releases)

## Installation

1. Install MelonLoader for the game and launch it once to generate its files.
2. Download `WarehouseHelper.dll` from [Releases](https://github.com/githubnano/probably-stolen-warehouse-helper/releases).
3. Exit the game and place the DLL in the game's `Mods` folder. Replace the previous copy when updating.
4. Restart the game.

This repository distributes only this mod's source and custom artwork. Compiled DLLs are distributed through Releases. Game files, MelonLoader, Unity/game assemblies, saves, and other mods are not included.

## Crafting

Use a screwdriver on a module extractor to dismantle it into a parts pile. Drag the required materials onto the pile to assemble a helper.

| Model | Dismantle | Additional materials (defaults) | Hotkeys |
| --- | --- | --- | --- |
| Basic | Basic module extractor | 1 junk, 1 electronic component | Alt+1–2 |
| Advanced | Advanced module extractor | 2 scrap metal, 4 electronic components | Alt+1–9 |

## Controls

- Drag an item or a selected group onto the helper. Choose an action, then a hotkey.
- Available actions depend on the item: use, equip, activate, toggle, open, unload, and move.
- A key can hold multiple items. Rebinding replaces that key's previous bindings. Group actions run on each eligible item and may consume multiple items.
- For movement, press the key to pick up the bound item or group. Left-click or press the same key again to place it. Right-click or press Esc to cancel and return it to its original position. The source container does not need to be open.
- To clear one item's binding, drag that item onto the helper by itself and choose **Clear Binding**. Bindings appear in item descriptions and persist in saves.
- Helper models limit which keys can be assigned. The helper need not remain present to execute existing bindings. Alt+number does not also trigger the original toolbox.

## Right-click menu

These chores belong to the **warehouse helper**: at least one helper (basic or advanced) must exist in the scene for them to run. Right-click the **helper** to open its control panel, or right-click the charger / machine / smuggler bay itself — both edit the same shared state.

- **Recharger auto swap**: cycle the mode (off / normal / large-capacity first). Each morning after sleeping (only while the power stayed on and batteries actually charged), every recharger swaps its full batteries for partially charged ones; each full battery lands where its replacement was. Batteries inside trash cans are never used.
- **Machine auto swap**: a global toggle. While enabled, every battery-powered machine is checked after each run (manual use or nightly processing); if it cannot power the next run, a replacement is installed automatically, preferring fully charged ones. You are notified once if no battery is available.
- **One-click stash**: move every piece of contraband (highest level first) and stolen goods (highest price first) not already inside a smuggler bay into the bays (mini / normal / modded / expanded all count). If space runs out, the leftover count is reported and those items are marked red — containers holding them are marked too, so you can find them without opening every box. Use **Put back** afterwards to return everything to its recorded place and clear the marks (records reset on scene change).

## Language and configuration

Follows the game's locale with Simplified Chinese, English, French, German, Italian, Japanese, Korean, Brazilian Portuguese, Russian, and Spanish. Item names, descriptions, menus, notices, assembly progress, and setting labels are localized. Existing items refresh when the language changes. Translations live in [Locales](src/WarehouseHelper/Locales).

After launching the game, edit `[WarehouseHelper]` in `UserData/MelonPreferences.cfg` to adjust ingredient IDs, quantities, and the hotkey modifier. Supported modifiers: `None`, `Shift`, `Ctrl`, and `Alt` (default). Item IDs used by the right-click features are configurable too: `id_recharger`, `id_smuggler_bays` (comma separated), and `id_trashcan`.

## Building

Requires the .NET 6 SDK and a local game installation that has already run with MelonLoader.

```powershell
./build.ps1 -GameDir "D:\Games\Probably Stolen Demo"
```

Use `-DotnetPath` to select a specific `dotnet.exe`. The script reads dependencies from your local game installation and writes `artifacts/WarehouseHelper.dll`; it does not install the mod automatically or bundle dependency assemblies.

## License

[MIT](LICENSE) for this repository's mod content. Game files and third-party dependencies are not distributed with it.
