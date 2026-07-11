using System;
using System.Collections.Generic;
using CyclopsScannerModule.Behaviours;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CyclopsScannerModule.UI;

/// <summary>
/// IMGUI resource-selection panel for the Cyclops Scanner Module. A singleton added once to
/// BepInEx's persistent manager GameObject by <c>Plugin.Awake</c>, so it survives scene loads;
/// it is opened, switched between Cyclops, or closed on demand via <see cref="Toggle"/> for
/// whichever <see cref="CyclopsScannerController"/> the player is currently inside.
/// The controller's Update is the only place the menu keybind is read — this class never polls
/// input itself, so a single key press can't open and close the menu within the same frame.
/// </summary>
public class ScannerMenu : MonoBehaviour
{
    private const float RefreshInterval = 2f;
    private const float WindowWidth = 320f;
    private const float WindowHeight = 400f;
    private const int WindowId = 0x5CA77E12;
    private const float ApproxRowHeight = 22f;

    public static ScannerMenu Instance { get; private set; }

    private CyclopsScannerController _owner;
    private Rect _windowRect;
    private Vector2 _scrollPos;
    private readonly List<(TechType Type, string Name)> _entries = new();
    private float _refreshTimer;
    private int _focusIndex;
    private bool _stickEngaged; // left-stick flick edge-detect: one move per push, must recenter to repeat

    private bool IsOpen => _owner != null;

    // Flat focus index space: resource rows first, then Stop scanning, then Close.
    private int FocusableCount => _entries.Count + 2; // resource rows + Stop + Close

    private void Awake()
    {
        Instance = this;
    }

    /// <summary>True if the menu is currently open for <paramref name="owner"/>.</summary>
    public static bool IsOpenFor(CyclopsScannerController owner)
    {
        return Instance != null && Instance._owner == owner;
    }

    /// <summary>
    /// Opens the menu for <paramref name="owner"/>; if already open for that same owner, closes it;
    /// if open for a different owner, switches context (and refreshes the list) without closing.
    /// </summary>
    public static void Toggle(CyclopsScannerController owner)
    {
        if (Instance == null)
        {
            Plugin.Logger.LogWarning("[Scanner] ScannerMenu.Toggle called before Instance was set.");
            return;
        }
        Instance.ToggleInternal(owner);
    }

    private void ToggleInternal(CyclopsScannerController owner)
    {
        if (_owner == owner)
        {
            Close();
        }
        else if (_owner != null)
        {
            // Menu is already open for a different Cyclops; switch context in place.
            _owner = owner;
            _refreshTimer = 0f;
            _focusIndex = 0;
            RefreshList();
        }
        else
        {
            Open(owner);
        }
    }

    private void Open(CyclopsScannerController owner)
    {
        _owner = owner;
        _windowRect = new Rect((Screen.width - WindowWidth) / 2f, (Screen.height - WindowHeight) / 2f, WindowWidth, WindowHeight);
        _scrollPos = Vector2.zero;
        _refreshTimer = 0f;
        _focusIndex = 0;
        RefreshList();
    }

    private void Close()
    {
        _owner = null;
        _entries.Clear();
        _scrollPos = Vector2.zero;
        UWE.Utils.lockCursor = true;
    }

    private void Update()
    {
        if (ReferenceEquals(_owner, null)) return; // menu closed

        // _owner == null here means Unity-destroyed (overloaded check): close properly.
        if (_owner == null
            || Player.main == null || Player.main.currentSub != _owner.Sub
            || !_owner.ModuleInstalled
            || Player.main.GetPDA().isInUse)
        {
            Close();
            return;
        }

        // The game forces the cursor locked every frame; keep overriding that while the menu is open.
        UWE.Utils.lockCursor = false;

        _refreshTimer += Time.deltaTime;
        if (_refreshTimer >= RefreshInterval)
        {
            _refreshTimer = 0f;
            RefreshList();
        }

        // Controller/keyboard focus navigation. We read raw keyboard + gamepad inputs directly
        // instead of GameInput UI actions: the game binds UISubmit to the primary interact/click,
        // so using it both misses the Enter key AND re-fires on every mouse click (which would
        // re-activate the highlighted row when you click "Close"). "Close" is a focusable item, so
        // we never use UICancel (which can also open the pause menu). The mouse path in DrawWindow
        // keeps working independently.
        int count = FocusableCount;
        _focusIndex = Mathf.Clamp(_focusIndex, 0, count - 1); // list size may have changed on refresh

        var pad = Gamepad.current;

        // Left stick as an alternative to the d-pad: treat a push past the engage threshold as one
        // discrete move, and require the stick to return near center before it moves again (so it
        // doesn't fly through the list). Up on the stick moves the highlight up.
        const float StickEngage = 0.6f;
        const float StickRelease = 0.35f;
        float stickY = pad != null ? pad.leftStick.ReadValue().y : 0f;
        bool stickUp = false, stickDown = false;
        if (Mathf.Abs(stickY) < StickRelease)
            _stickEngaged = false;
        else if (!_stickEngaged && Mathf.Abs(stickY) >= StickEngage)
        {
            _stickEngaged = true;
            stickUp = stickY > 0f;
            stickDown = stickY < 0f;
        }

        bool navDown = Input.GetKeyDown(KeyCode.DownArrow) || stickDown || (pad != null && pad.dpad.down.wasPressedThisFrame);
        bool navUp = Input.GetKeyDown(KeyCode.UpArrow) || stickUp || (pad != null && pad.dpad.up.wasPressedThisFrame);
        bool submit = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)
            || (pad != null && pad.buttonSouth.wasPressedThisFrame);

        if (navDown)
            _focusIndex = (_focusIndex + 1) % count;
        else if (navUp)
            _focusIndex = (_focusIndex - 1 + count) % count;

        if (submit)
        {
            ActivateFocused();
            return; // ActivateFocused may have called Close()
        }

        // Keep the focused resource row roughly visible in the scroll view.
        if (_focusIndex < _entries.Count)
            _scrollPos.y = Mathf.Max(0f, _focusIndex * ApproxRowHeight - WindowHeight * 0.4f);
    }

    private void ActivateFocused()
    {
        if (_owner == null) return;
        if (_focusIndex < _entries.Count)
            _owner.StartScanning(_entries[_focusIndex].Type);
        else if (_focusIndex == _entries.Count)
            _owner.StopScanning();
        else
            Close();
    }

    private void RefreshList()
    {
        _entries.Clear();
        if (_owner == null || _owner.Sub == null)
            return;

        var types = new List<TechType>();
        ResourceTrackerDatabase.GetTechTypesInRange(_owner.Sub.transform.position, CyclopsScannerController.ScanRange, types);

        foreach (var tt in types)
            _entries.Add((tt, GetDisplayName(tt)));

        _entries.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetDisplayName(TechType techType)
    {
        return Language.main != null ? Language.main.Get(techType.AsString(false)) : techType.ToString();
    }

    private void OnGUI()
    {
        if (!IsOpen) return;
        _windowRect = GUILayout.Window(WindowId, _windowRect, DrawWindow, "Cyclops Scanner",
            GUILayout.Width(WindowWidth), GUILayout.Height(WindowHeight));
    }

    private bool FocusButton(string label, int index)
    {
        Color prev = GUI.backgroundColor;
        if (index == _focusIndex)
            GUI.backgroundColor = new Color(0.35f, 0.75f, 1f); // focus highlight
        bool clicked = GUILayout.Button(label);
        GUI.backgroundColor = prev;
        return clicked;
    }

    private void DrawWindow(int id)
    {
        string status;
        if (_owner.PowerPaused)
            status = "Paused — insufficient power";
        else if (_owner.ScanActive && _owner.SelectedType != TechType.None)
            status = $"Scanning: {GetDisplayName(_owner.SelectedType)}";
        else
            status = "Idle";
        GUILayout.Label(status);

        if (_entries.Count == 0)
        {
            GUILayout.Label("No trackable resources within 300m");
        }
        else
        {
            _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.ExpandHeight(true));
            for (int i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                string label = entry.Type == _owner.SelectedType ? "► " + entry.Name : entry.Name;
                if (FocusButton(label, i))
                    _owner.StartScanning(entry.Type);
            }
            GUILayout.EndScrollView();
        }

        GUILayout.BeginHorizontal();
        if (FocusButton("Stop scanning", _entries.Count))
            _owner.StopScanning();
        if (FocusButton("Close", _entries.Count + 1))
            Close();
        GUILayout.EndHorizontal();

        GUI.DragWindow();
    }
}
