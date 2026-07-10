using System.Collections.Generic;
using CyclopsScannerModule.Items;
using CyclopsScannerModule.SaveData;
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

    private void Start()
    {
        RestoreFromSave();
    }

    private void OnDestroy()
    {
        WriteToSave();
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

        // 2. Keybind: toggle the resource-selection menu.
        if (Input.GetKeyDown(Plugin.MenuKey.Value)
            && Player.main != null && Player.main.currentSub == _sub
            && ModuleInstalled
            && !Player.main.GetPDA().isInUse
            && AvatarInputHandler.main != null && AvatarInputHandler.main.IsEnabled()
            && Time.timeScale > 0f)
        {
            UI.ScannerMenu.Toggle(this);
        }

        // TEMP DEBUG (remove in step 6): L toggles scanning Titanium for quick in-game blip testing,
        // with on-screen state dump so failures are diagnosable without log files.
        if (Input.GetKeyDown(KeyCode.L)
            && Player.main != null && Player.main.currentSub == _sub)
        {
            ErrorMessage.AddMessage(
                $"[Scanner] installed={ModuleInstalled} active={ScanActive} type={SelectedType} paused={PowerPaused} console={(_sub.upgradeConsole != null)}");
            if (ModuleInstalled)
            {
                if (ScanActive) StopScanning();
                else StartScanning(TechType.Titanium);
                ErrorMessage.AddMessage($"[Scanner] now active={ScanActive} type={SelectedType}");
            }
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

    /// <summary>Writes this sub's scanner state into the save cache (called on save and on destroy).</summary>
    internal void WriteToSave()
    {
        string id = PrefabId;
        if (id == null || Plugin.SaveState == null)
            return;
        Plugin.SaveState.Subs[id] = new SubScannerState
        {
            SelectedTechType = SelectedType.AsString(),
            ScanActive = ScanActive,
        };
    }

    /// <summary>Applies persisted state for this sub, if any. Safe to call more than once.</summary>
    internal void RestoreFromSave()
    {
        string id = PrefabId;
        if (id == null || Plugin.SaveState == null)
            return;
        if (!Plugin.SaveState.Subs.TryGetValue(id, out var state) || state == null)
            return;

        TechType techType = TechType.None;
        if (!string.IsNullOrEmpty(state.SelectedTechType))
            System.Enum.TryParse(state.SelectedTechType, true, out techType);

        SelectedType = techType;
        ScanActive = state.ScanActive && techType != TechType.None;
        PowerPaused = false; // drain loop re-pauses next frame if power is still short
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
