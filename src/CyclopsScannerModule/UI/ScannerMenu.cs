using System;
using System.Collections.Generic;
using CyclopsScannerModule.Behaviours;
using UnityEngine;

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

    public static ScannerMenu Instance { get; private set; }

    private CyclopsScannerController _owner;
    private Rect _windowRect;
    private Vector2 _scrollPos;
    private readonly List<(TechType Type, string Name)> _entries = new();
    private float _refreshTimer;

    private bool IsOpen => _owner != null;

    private void Awake()
    {
        Instance = this;
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
        if (!IsOpen) return;

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
            foreach (var entry in _entries)
            {
                string label = entry.Type == _owner.SelectedType ? "► " + entry.Name : entry.Name;
                if (GUILayout.Button(label))
                    _owner.StartScanning(entry.Type);
            }
            GUILayout.EndScrollView();
        }

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Stop scanning"))
            _owner.StopScanning();
        if (GUILayout.Button("Close"))
            Close();
        GUILayout.EndHorizontal();

        GUI.DragWindow();
    }
}
