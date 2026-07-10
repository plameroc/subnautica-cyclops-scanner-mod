# Cyclops Scanner Module

A [BepInEx](https://github.com/BepInEx/BepInEx) mod for Subnautica that adds Scanner Room-style
resource scanning/pinging functionality to the Cyclops as an equippable upgrade module.

Built on [Nautilus](https://github.com/SubnauticaModding/Nautilus), the community Subnautica
modding API.

## Status

Workspace scaffold only — no gameplay features implemented yet.

## Prerequisites

- [.NET SDK](https://dotnet.microsoft.com/download) 8.0+
- Subnautica (original, not Below Zero) with [BepInEx](https://github.com/toebeann/BepInEx.Subnautica)
  installed into the game folder

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

## Project layout

- `src/CyclopsScannerModule/` — the mod project
  - `Plugin.cs` — BepInEx plugin entry point
- `CyclopsScannerModule.sln` — solution file

## License

MIT — see [LICENSE](LICENSE).
