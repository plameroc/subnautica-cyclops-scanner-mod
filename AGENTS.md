# Agent notes: Cyclops Scanner Module

A BepInEx mod for the original Subnautica (not Below Zero) that adds a craftable Cyclops
upgrade module with Scanner Room-style resource tracking. See [README.md](README.md) for
player-facing docs. This file is for agents making changes to the mod itself.

## Build & deploy

- `dotnet build` from the repo root works because it resolves `CyclopsScannerModule.sln` (there's
  no `.csproj` at the root, only nested under `src/`) — **don't delete the `.sln`**, the README's
  documented build command depends on it.
- Target: `net472`, C# 11, BepInEx 5.4.23 (Mono backend, not IL2CPP).
- `Subnautica.GameLibs` (publicized game assemblies) and `Subnautica.Nautilus` are both ordinary
  NuGet packages from public feeds (`nuget.org` + `nuget.bepinex.dev`, declared in the `.csproj`
  via `RestoreAdditionalProjectSources`) — building requires **no local Subnautica install**.
  Verified by a clean GitHub Actions Windows runner build.
- Nautilus is needed **twice**: as a NuGet compile reference, and separately as an actual runtime
  plugin installed in the target game's `BepInEx\plugins\Nautilus\` (the `_SN` build, not `_BZ`).
  Missing the runtime copy fails with `missing dependencies: com.snmodding.nautilus` at game load.
- Local test deploys: a post-build `DeployToGame` MSBuild target copies the DLL to
  `$(SubnauticaGameDir)\BepInEx\plugins\CyclopsScannerModule\`. `SubnauticaGameDir` lives in
  `src/CyclopsScannerModule/CyclopsScannerModule.csproj.user` (gitignored, machine-specific — the
  SDK auto-imports `.user` files; don't add an explicit `<Import>`, it causes MSB4011).
- Windows PowerShell/Git-Bash sessions opened before a tool (git/dotnet/gh) was installed won't
  see it on PATH until the shell restarts. VS Code's integrated terminal is worse: it inherits
  PATH from the VS Code *process*, so even a brand-new terminal tab stays stale until VS Code
  itself is fully restarted (not just "Reload Window").

## Release process

Push a tag matching `v*.*.*` (e.g. `git tag -a v1.0.2 -m "..."` then `git push origin v1.0.2`) and
`.github/workflows/release.yml` builds Release config, stamps `-p:Version=<tag without v>` into
the assembly and BepInEx plugin metadata, zips it as `CyclopsScannerModule-<version>.zip`
(pre-laid-out as `CyclopsScannerModule/CyclopsScannerModule.dll` so it extracts straight into
`BepInEx\plugins\`), and publishes a GitHub Release with auto-generated notes. A manual
`workflow_dispatch` run builds and packages but deliberately skips releasing — use it to
smoke-test the pipeline without cluttering the releases list.

## Architecture

- **`Items/ScannerModuleItem.cs`** — Nautilus registration: clones `CyclopsThermalReactorModule`,
  sets `EquipmentType.CyclopsModule` (not `SetVehicleUpgradeModule`, which Nautilus explicitly
  doesn't support for the Cyclops), adds it to the Cyclops fabricator's flat craft tree, unlocks
  alongside `TechType.BaseMapRoom`.
- **`Behaviours/CyclopsScannerController.cs`** — one instance per Cyclops (added by
  `Patches/SubRootPatches.cs` via a `SubRoot.Start` postfix, guarded on `__instance.isCyclops`
  since `SubRoot` is also the base of `BaseRoot`). Polls `Equipment.GetCount` per frame rather than
  event-patching module install/removal. Owns power drain and save/load.
- **`Patches/ResourceTrackerPatches.cs`** — the actual scanning mechanism: a single Harmony
  postfix on `uGUI_ResourceTracker.GatherScanned`, which is vanilla's one choke point for
  populating HUD blips from scanner rooms. For every operational controller within 500m of the
  camera, it calls `ResourceTrackerDatabase.GetNodes(...)` into the same `HashSet` vanilla uses —
  free deduplication against real scanner rooms, and free HUD-chip gating, since
  `uGUI_ResourceTracker.IsVisibleNow()` already gates on
  `Inventory.main.equipment.GetCount(TechType.MapRoomHUDChip) > 0` with no patch needed.
- **`Behaviours/ScannerHandTarget.cs`** — in-world interaction box (controller/Steam-Deck path to
  open the menu, no keybind needed). Must be `isTrigger = true` **and** on `LayerID.Useable` —
  `Targeting.Filter` silently discards trigger colliders on any other layer, so this is an easy
  way to add a hand target that never appears if you get the layer wrong.
- **`UI/ScannerMenu.cs`** — singleton OnGUI panel on the BepInEx plugin's `DontDestroyOnLoad`
  GameObject. Freezes `PlayerController.inputEnabled` while open (not `PlayerController.SetEnabled`,
  which also stops the motor/gravity tracking) so the gamepad stick only drives menu navigation,
  not player movement. Navigation reads raw `KeyCode`/`Gamepad.current` input directly — **not**
  `GameInput.Button.UISubmit`, which is aliased to the primary mouse-click/interact action and
  double-fires on every click.
- **`SaveData/ScannerSaveData.cs`** — per-save-slot persistence via Nautilus `SaveDataCache`,
  keyed by each Cyclops's `PrefabIdentifier.Id`.

## Locked design decisions

Don't change these without the repo owner's say-so — they were explicit product decisions, not
defaults:

- Flat **12 energy/minute** drain while scanning, regardless of installed power cell type (ion
  cells do *not* reduce the drain — this was a deliberate override of the more "natural" idea of
  scaling drain by max power).
- Fixed 300m scan range, not configurable.
- No stacking benefit — one module or several, behavior is identical.
- All matching resources reveal instantly on selection; no scanner-room-style progressive sweep.
- No holographic projection — HUD blips only, via the vanilla scanner-room system.
- Standalone; no dependency on MoreCyclopsUpgrades.

## Verified game facts (from IL of the installed build)

These took real effort to pin down (Mono.Cecil dumps of the installed game's assemblies via
BepInEx's own `Mono.Cecil.dll`) — trust them rather than re-deriving from scratch:

- `PowerSystem.ConsumeEnergy(IPowerInterface, float amount, out float consumed) : bool` is the
  correct static API for draining a `SubRoot.powerRelay`. There is no `PowerRelay.ConsumeEnergy`.
- `GameModeUtils.RequiresPower()` is `false` in Creative — gate power drain on it.
- `AvatarInputHandler.IsEnabled()` is `activeInHierarchy && UWE.Utils.lockCursor` — unlocking the
  cursor (e.g. for an OnGUI panel) implicitly disables keyboard/mouse gameplay input handling.
  This caused two real bugs this session (menu couldn't be closed by its own keybind; the temp
  debug key silently stopped firing) — always account for this when a menu or panel unlocks the
  cursor.
- `IHandTarget` is `OnHandHover(GUIHand)` / `OnHandClick(GUIHand)`. The reticle raycast
  (`Targeting.GetTarget`/`Filter`) only accepts a hit collider if it's a **trigger** on the
  **`LayerID.Useable`** layer; anything else is silently discarded, no error.
- `GameInput.Button.UISubmit` is bound to the primary interact/mouse-click action, not Enter —
  don't use it for "activate the focused UI element" unless you also want every mouse click to
  re-trigger it.
