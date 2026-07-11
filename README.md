# Cyclops Scanner Module

A [BepInEx](https://github.com/BepInEx/BepInEx) mod for Subnautica that adds Scanner Room-style
resource scanning/pinging functionality to the Cyclops as an equippable upgrade module.

Built on [Nautilus](https://github.com/SubnauticaModding/Nautilus), the community Subnautica
modding API.

## Features

- **Cyclops Scanner Module** — a new Cyclops upgrade, craftable at the upgrade fabricator inside
  the Cyclops engine room. Fits any standard Cyclops upgrade slot.
- **Blueprint unlock**: unlocks together with the Scanner Room blueprint.
- **Recipe**: 1x Computer Chip, 2x Magnetite, 1x Copper Wire.
- **Resource selection menu**: open a panel listing every trackable resource type within range,
  then pick one to start scanning. Blips update as you cruise into newly loaded terrain. Open it
  either way:
  - Press <kbd>K</kbd> (rebindable) while inside a Cyclops with the module installed.
  - Look at the wall panel **opposite the upgrade console** in the engine room (aimed at the small
    yellow triangle) and press the interact button — controller / Steam Deck friendly, no keybind
    needed.
- **Controller & Steam Deck support**: the menu is fully navigable without a mouse — the d-pad,
  left stick, or arrow keys move the highlight, A / <kbd>Enter</kbd> selects, and "Close" is a
  selectable item. Mouse and clicks still work in parallel.
- **HUD blips**: detected resources appear on your HUD through the game's own scanner-room blip
  system — you need the **Scanner Room HUD Chip** equipped to see them, exactly like a scanner
  room. No holographic map; scan range is 300m around the sub.
- **Power**: drains a flat **12 energy per minute** from the Cyclops while actively scanning
  (1%/min of a standard 6x Power Cell loadout — deliberately not scaled by ion/upgraded cells).
  When the sub runs dry, scanning pauses and auto-resumes once power recovers. No drain in
  Creative mode.
- **Persistence**: each Cyclops remembers its own selection and scanning state across save/load.
  Multiple Cyclops operate independently.

## Installing (players)

1. Install [BepInEx for Subnautica](https://github.com/toebeann/BepInEx.Subnautica) and
   [Nautilus](https://github.com/SubnauticaModding/Nautilus/releases) (see runtime note below).
2. Drop `CyclopsScannerModule.dll` into `Subnautica\BepInEx\plugins\CyclopsScannerModule\`.
3. The menu key can be rebound in `BepInEx\config\com.plameroc.cyclopsscannermodule.cfg`
   (created after first launch).

## Prerequisites (building from source)

- [.NET SDK](https://dotnet.microsoft.com/download) 8.0+
- Subnautica (original, not Below Zero) with [BepInEx](https://github.com/toebeann/BepInEx.Subnautica)
  installed into the game folder
- [Nautilus](https://github.com/SubnauticaModding/Nautilus) installed as a plugin in the game folder
  (`BepInEx\plugins\Nautilus\`) — **required at runtime**, separately from the `Subnautica.Nautilus`
  NuGet package this project compiles against. Without it, BepInEx will refuse to load this mod
  with `missing dependencies: com.snmodding.nautilus`. Grab the `Nautilus_SN.STABLE_*.zip` asset
  (the `_SN` build, not `_BZ`) from the [releases page](https://github.com/SubnauticaModding/Nautilus/releases)
  and extract it into `BepInEx\` so it lands at `BepInEx\plugins\Nautilus\`. Match the version to the
  `Subnautica.Nautilus` package version in the `.csproj` when possible.

## Building

```powershell
dotnet build
```

To have the build automatically copy the plugin DLL into your local Subnautica install for testing,
create `src/CyclopsScannerModule/CyclopsScannerModule.csproj.user` (gitignored, machine-specific) with:

```xml
<Project>
  <PropertyGroup>
    <SubnauticaGameDir>C:\Path\To\Subnautica</SubnauticaGameDir>
  </PropertyGroup>
</Project>
```

The built `CyclopsScannerModule.dll` will be copied to `BepInEx\plugins\CyclopsScannerModule\` under
that directory after every build.

For a distributable DLL, build the Release configuration; the output lands in
`src/CyclopsScannerModule/bin/Release/net472/CyclopsScannerModule.dll`:

```powershell
dotnet build -c Release
```

## Project layout

- `src/CyclopsScannerModule/` — the mod project
  - `Plugin.cs` — BepInEx plugin entry point: config binding, item/save registration, Harmony
  - `Items/ScannerModuleItem.cs` — Nautilus registration of the upgrade module (recipe, craft
    node, unlock, equipment type)
  - `Behaviours/CyclopsScannerController.cs` — per-Cyclops state machine: module detection,
    menu keybind, power drain, scan state
  - `Patches/SubRootPatches.cs` — attaches the controller to every Cyclops
  - `Patches/ResourceTrackerPatches.cs` — feeds our scan results into the vanilla HUD blip system
  - `UI/ScannerMenu.cs` — the resource-selection panel (IMGUI)
  - `SaveData/ScannerSaveData.cs` — per-save-slot persistence via Nautilus
- `CyclopsScannerModule.sln` — solution file

## License

MIT — see [LICENSE](LICENSE).
