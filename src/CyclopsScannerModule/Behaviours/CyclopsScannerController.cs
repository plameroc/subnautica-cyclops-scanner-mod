using System.Collections.Generic;
using CyclopsScannerModule.Items;
using CyclopsScannerModule.SaveData;
using UnityEngine;

namespace CyclopsScannerModule.Behaviours;

/// <summary>
/// Per-Cyclops controller. Attached to the Cyclops root GameObject (alongside <see cref="SubRoot"/>)
/// by <see cref="Patches.SubRoot_Start_Patch"/>. Tracks whether a Cyclops Scanner Module is installed,
/// what resource the player has selected to scan for, and drains power while actively scanning.
/// </summary>
public class CyclopsScannerController : MonoBehaviour
{
    public static readonly List<CyclopsScannerController> All = new();

    public const float ScanRange = 300f;
    public const float DrainPerMinute = 12f; // flat 12 energy/min — intentionally NOT scaled by max power
    private const float ResumePowerThreshold = 12f; // one minute's worth; hysteresis so it doesn't flicker

    // Tunable placement — final values set during in-game placement session (step: placement).
    private static readonly Vector3 InteractLocalPosition = new Vector3(0f, 1.4f, 0f);
    private static readonly Vector3 InteractBoxSize = new Vector3(0.6f, 0.6f, 0.4f);

    private SubRoot _sub;
    private PrefabIdentifier _prefabId;
    private GameObject _handTarget;
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
        CreateHandTarget();
    }

    private void CreateHandTarget()
    {
        _handTarget = new GameObject("CyclopsScannerInteract");
        _handTarget.transform.SetParent(transform, false);
        _handTarget.transform.localPosition = InteractLocalPosition;
        _handTarget.transform.localRotation = Quaternion.identity;
        // Targeting.Filter accepts a TRIGGER collider ONLY when it's on the Useable layer
        // (a trigger on any other layer is silently discarded). This is the standard interactable setup.
        _handTarget.layer = LayerID.Useable;
        var box = _handTarget.AddComponent<BoxCollider>();
        box.isTrigger = true; // trigger => doesn't block player movement, still hit by reticle (QueryTriggerInteraction.Collide)
        box.size = InteractBoxSize;
        var ht = _handTarget.AddComponent<ScannerHandTarget>();
        ht.Owner = this;
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
            ForceBlipRefresh();
        }
        ModuleInstalled = installed;

        // 2. Keybind: toggle the resource-selection menu. Closing uses relaxed guards: the open
        // menu unlocks the cursor, which disables AvatarInputHandler, so the open-guards below
        // can never pass while the menu is up and the same key couldn't close it.
        if (Input.GetKeyDown(Plugin.MenuKey.Value)
            && Player.main != null && Player.main.currentSub == _sub)
        {
            if (UI.ScannerMenu.IsOpenFor(this))
            {
                UI.ScannerMenu.Toggle(this);
            }
            else if (ModuleInstalled
                && !Player.main.GetPDA().isInUse
                && AvatarInputHandler.main != null && AvatarInputHandler.main.IsEnabled()
                && Time.timeScale > 0f)
            {
                UI.ScannerMenu.Toggle(this);
            }
        }

        // TEMP (remove after placement): press Semicolon while inside this sub to log the local-space
        // point the camera is looking at (up to 5m away) and snap the hand target there for a quick
        // visual check. Used to find good InteractLocalPosition/InteractBoxSize values in-game.
        if (Input.GetKeyDown(KeyCode.Semicolon)
            && Player.main != null && Player.main.currentSub == _sub)
        {
            var cam = MainCamera.camera;
            if (cam != null && Physics.Raycast(cam.transform.position, cam.transform.forward, out RaycastHit hit, 5f))
            {
                Vector3 local = transform.InverseTransformPoint(hit.point);
                ErrorMessage.AddMessage($"[Scanner placement] localPos = ({local.x:F3}, {local.y:F3}, {local.z:F3})");
                if (_handTarget != null) _handTarget.transform.localPosition = local;
                ErrorMessage.AddMessage("[Scanner placement] box moved — look at it to confirm the prompt appears");
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
                    ForceBlipRefresh();
                }
            }
            else if (_sub.powerRelay != null)
            {
                float amount = DrainPerMinute / 60f * Time.deltaTime;
                if (!PowerSystem.ConsumeEnergy(_sub.powerRelay, amount, out _))
                {
                    PowerPaused = true;
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
        ForceBlipRefresh();
    }

    public void StopScanning()
    {
        ScanActive = false;
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
