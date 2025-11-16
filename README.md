# Reference Replacement Mod

A [ResoniteModLoader](https://github.com/resonite-modding-group/ResoniteModLoader) mod for [Resonite](https://resonite.com/) that provides a userspace dialog for bulk replacement of references. Find and replace all references pointing to one element with references to another element within a slot hierarchy, with analysis preview and undo support.

Launch the tool via `Create New > Editor > Reference Replacement (Mod)` in the Dev Create menu.

## Installation

1. Install [ResoniteModLoader](https://github.com/resonite-modding-group/ResoniteModLoader).
2. Download the latest `ReferenceReplacement.dll` from this repo’s Releases page and place it in the `rml_mods` folder inside your Resonite install (e.g., `C:\Program Files (x86)\Steam\steamapps\common\Resonite\rml_mods`). Create the folder if it does not exist.
3. Launch Resonite and confirm the mod loads via the in-game logs if desired.

## Build & Hot Reload

1. Install the .NET 9 SDK.
2. `dotnet build ReferenceReplacement.sln` auto-detects the Resonite install next to this repo, the default Steam Windows path, then the default Steam Linux path. If the game lives elsewhere, pass `-p:ResonitePath="/absolute/path/to/Resonite"` so the build can find `FrooxEngine.dll`, `Elements.Core.dll`, `Libraries/ResoniteModLoader.dll`, and `rml_libs/0Harmony.dll`.
3. Set `CopyToMods=true` when invoking `dotnet build` to copy the compiled DLL into `$(ResonitePath)/rml_mods` after each build.
4. Drop `ResoniteHotReloadLib.dll` (and `ResoniteHotReloadLibCore.dll`) into `$(ResonitePath)/rml_libs` and build with `-p:EnableResoniteHotReloadLib=true` if you want the Dev Tool’s **Hot Reload Mods** panel to reload this mod without restarting Resonite. Leave the property unset on machines without the DLL.
