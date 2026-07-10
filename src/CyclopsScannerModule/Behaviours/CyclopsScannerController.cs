using System.Collections.Generic;
using CyclopsScannerModule.Items;
using UnityEngine;

namespace CyclopsScannerModule.Behaviours;

/// <summary>
/// Per-Cyclops controller. Attached to the Cyclops root GameObject (alongside <see cref="SubRoot"/>)
/// by <see cref="Patches.SubRootPatches"/>. Tracks whether a Cyclops Scanner Module is installed,
/// what resource the player has selected to scan for, and drains power while actively scanning.
/// </summary>
public class CyclopsScannerController : MonoBehaviour
{
    public static readonly List<CyclopsScannerController> All = new();

    public const float ScanRange = 300f;
    public const float DrainPerMinute = 12f; // flat 12 energy/min — intentionally NOT scaled by max power
    private const float ResumePowerThreshold = 12f; // one minute's worth; hysteresis so it doesn't flicker

    private SubRoot _sub;
    private PrefabIdentifier _prefabId;
    private static uGUI_ResourceTracker _tracker; // cached lazily

    public SubRoot Sub => _sub;
    public string PrefabId => _prefabId != null ? _prefabId.Id : null;
    public TechType SelectedType { get; private set; } // TechType.None = nothing selected
    public bool ScanActive { get; private set; } // user intent; survives power loss
    public bool PowerPaused { get; private set; }
    public bool ModuleInstalled { get; private set; }

    public bool IsOperational => ModuleInstalled && ScanActive && SelectedType != TechType.None
        && !PowerPaused && _sub != null && _sub.live != null && _sub.live.IsAlive();

    private void Awake()
    {
        _sub = GetComponent<SubRoot>();
        _prefabId = GetComponent<PrefabIdentifier>() ?? GetComponentInParent<PrefabIdentifier>();
        if (_prefabId == null)
            Plugin.Logger.LogWarning("[Scanner] CyclopsScannerController.Awake: no PrefabIdentifier found on or above this GameObject.");
    }

    private void OnEnable()
    {
        All.Add(this);
    }

    private void OnDisable()
    {
        All.Remove(this);
    }

    private void Update()
    {
        // 1. Poll module presence.
        bool installed = _sub != null && _sub.upgradeConsole != null && _sub.upgradeConsole.modules != null
            && _sub.upgradeConsole.modules.GetCount(ScannerModuleItem.Info.TechType) > 0;
        if (ModuleInstalled && !installed)
        {
            Plugin.Logger.LogDebug("[Scanner] Module removed from Cyclops.");
            ForceBlipRefresh();
        }
        ModuleInstalled = installed;

        // 2. Keybind (menu wiring comes in a later step).
        if (Input.GetKeyDown(Plugin.MenuKey.Value)
            && Player.main != null && Player.main.currentSub == _sub
            && ModuleInstalled
            && !Player.main.GetPDA().isInUse
            && AvatarInputHandler.main != null && AvatarInputHandler.main.IsEnabled()
            && Time.timeScale > 0f)
        {
            Plugin.Logger.LogDebug("[Scanner] Menu key pressed (menu not yet implemented)");
            // TODO(step 4): open ScannerMenu
        }

        // TEMP DEBUG (remove in step 6): L toggles scanning Titanium for quick in-game blip testing.
        if (Input.GetKeyDown(KeyCode.L)
            && Player.main != null && Player.main.currentSub == _sub
            && ModuleInstalled)
        {
            if (ScanActive) StopScanning();
            else StartScanning(TechType.Titanium);
        }

        // 3. Power drain.
        if (ModuleInstalled && ScanActive && SelectedType != TechType.None
            && _sub.live != null && _sub.live.IsAlive())
        {
            if (!GameModeUtils.RequiresPower())
            {
                PowerPaused = false;
            }
            else if (PowerPaused)
            {
                if (_sub.powerRelay != null && _sub.powerRelay.GetPower() >= ResumePowerThreshold)
                {
                    PowerPaused = false;
                    Plugin.Logger.LogDebug("[Scanner] Power resumed; scan unpaused.");
                    ForceBlipRefresh();
                }
            }
            else if (_sub.powerRelay != null)
            {
                float amount = DrainPerMinute / 60f * Time.deltaTime;
                if (!PowerSystem.ConsumeEnergy(_sub.powerRelay, amount, out _))
                {
                    PowerPaused = true;
                    Plugin.Logger.LogDebug("[Scanner] Out of power; scan paused.");
                    ForceBlipRefresh();
                }
            }
        }
    }

    public void StartScanning(TechType techType)
    {
        SelectedType = techType;
        ScanActive = techType != TechType.None;
        PowerPaused = false;
        Plugin.Logger.LogDebug($"[Scanner] StartScanning: {techType}");
        ForceBlipRefresh();
    }

    public void StopScanning()
    {
        ScanActive = false;
        Plugin.Logger.LogDebug("[Scanner] StopScanning");
        ForceBlipRefresh();
    }

    public static void ForceBlipRefresh()
    {
        if (_tracker == null)
            _tracker = Object.FindObjectOfType<uGUI_ResourceTracker>();
        if (_tracker != null)
            _tracker.gatherNextTick = true;
    }
}
