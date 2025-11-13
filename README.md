# Reference Replacement Mod

A ResoniteModLoader plugin that replicates the built-in Asset Optimization workflow for bulk reference swapping. It scans a slot hierarchy, finds every `ISyncRef` that currently points to a selected source `IWorldElement`, and rewrites the reference to a replacement target inside a single undo batch.

## Features
- Dev Tool context-menu entry so you can open the dialog directly from the inspector/dash (“Reference Replacement…”).
- Modal UI in userspace with reference pickers for the process root slot plus source/target references.
- Dry-run analysis button that reports how many matches (and incompatibilities) were discovered before you commit the change.
- Replacement button that writes every compatible `ISyncRef` inside one undo batch for clean rollback.
- Skips refs whose target type is incompatible with the requested replacement and reports how many were skipped.

## Building
1. Install the .NET 9 SDK.
2. Point the build at your Resonite installation by passing a property when invoking any `dotnet` command:
   ```bash
   dotnet build ReferenceReplacement.sln \
     -p:ResoniteAssembliesDir="/absolute/path/to/Resonite"
   ```
   The property must contain the folder with `FrooxEngine.dll`, `Elements.Core.dll`, `rml_libs/0Harmony.dll`, and `Libraries/ResoniteModLoader.dll`. The repo never stores those binaries.
3. (Optional) Set `CopyToMods=true` to copy the built DLL into `$(ResoniteAssembliesDir)/rml_mods` after each build.
4. (Optional) To enable hot reload during local development, place `ResoniteHotReloadLib.dll` (and its companion `ResoniteHotReloadLibCore.dll`) under `$(ResoniteAssembliesDir)/rml_libs` and build with `-p:EnableResoniteHotReloadLib=true`. Leave the property unset (default) if the extra assemblies are not available.

### Hot Reload (Optional)
- Builds compiled with `-p:EnableResoniteHotReloadLib=true` register the mod with [ResoniteHotReloadLib](https://github.com/Nytra/ResoniteHotReloadLib), enabling the Dev Tool’s **Hot Reload Mods** panel to reload it without restarting Resonite.
- Keep the property unset for CI or clean machines: the mod skips referencing the library entirely, so contributors without the DLL can still build and test normally.

## Usage
1. Equip the Dev Tool, target any slot, and open its context menu (default `T`).
2. Choose **Reference Replacement…** to spawn the dialog in userspace.
3. Use the three reference fields to pick:
   - Process root (slot): the slot whose subtree should be scanned.
   - Source reference: any slot, component, or other `IWorldElement` you wish to replace.
   - Replacement reference: the new `IWorldElement` all matching `ISyncRef`s should target.
4. Press **Analyze** to preview counts. Press **Replace** to run the swap inside a single undo batch.
5. Close the dialog when finished.

## Design Summary
- **Entry point:** A Harmony postfix on `DevTool.GenerateMenuItems` adds the context menu button and funnels requests to a singleton dialog manager so only one window per user exists.
- **UI layer:** A dedicated `ReferenceReplacementDialog` component builds the Asset Optimization style panel with standard `RefMemberEditor` fields, local-only button events, and status readouts.
- **Scanning:** `ReferenceScanner` walks the process root, every child slot, and every component’s sync members. It recursively descends through `ISyncRef`, `SyncRefList`, `SyncRefDictionary`, and other enumerable sync containers, capturing every reference that matches the selected source. Paths are tracked for reporting and debugging.
- **Safety:** Before applying changes, the dialog verifies that root/source/replacement live in the same world and that the source differs from the replacement. Replacement happens within an undo batch (`UndoManagerExtensions.BeginUndoBatch/EndUndoBatch`) so the user can revert in one step, and every incompatible reference is skipped with a counter in the status panel.

## Repository Layout
```
ReferenceReplacementMod/
├── ReferenceReplacement.sln
├── README.md
└── src/ReferenceReplacement/
    ├── ReferenceReplacement.csproj
    ├── ReferenceReplacementMod.cs
    ├── Logic/ReferenceScanner.cs
    ├── Patching/DevToolMenuPatch.cs
    └── UI/
        ├── ReferenceReplacementDialogManager.cs
        └── ReferenceReplacementDialog.cs
```
