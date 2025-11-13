# Reference Replacement Mod

A [ResoniteModLoader](https://github.com/resonite-modding-group/ResoniteModLoader) mod for [Resonite](https://resonite.com/) that mirrors the Asset Optimization workflow to bulk swap every matching `ISyncRef` inside a single undo batch.

Launch the tool via `Create New > Editor > Reference Replacement (Mod)` in the Dev Create menu.

## Installation

1. Install [ResoniteModLoader](https://github.com/resonite-modding-group/ResoniteModLoader).
2. Download the latest `ReferenceReplacement.dll` from this repo’s Releases page and place it in the `rml_mods` folder inside your Resonite install (e.g., `C:\Program Files (x86)\Steam\steamapps\common\Resonite\rml_mods`). Create the folder if it does not exist.
3. Launch Resonite and confirm the mod loads via the in-game logs if desired.

## Build & Hot Reload

1. Install the .NET 9 SDK.
2. `dotnet build ReferenceReplacement.sln` auto-detects `Resonite` next to the repo, the default Steam Windows path, then the default Steam Linux path. If the game lives elsewhere, pass `-p:ResoniteAssembliesDir="/absolute/path/to/Resonite"` so the build can find `FrooxEngine.dll`, `Elements.Core.dll`, `Libraries/ResoniteModLoader.dll`, and `rml_libs/0Harmony.dll`.
3. Set `CopyToMods=true` when invoking `dotnet build` to copy the compiled DLL into `$(ResoniteAssembliesDir)/rml_mods` after each build.
4. Drop `ResoniteHotReloadLib.dll` (and `ResoniteHotReloadLibCore.dll`) into `$(ResoniteAssembliesDir)/rml_libs` and build with `-p:EnableResoniteHotReloadLib=true` if you want the Dev Tool’s **Hot Reload Mods** panel to reload this mod without restarting Resonite. Leave the property unset on machines without the DLL.
