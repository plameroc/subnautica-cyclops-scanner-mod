using System.Collections.Generic;
using Nautilus.Json;
using Nautilus.Json.Attributes;

namespace CyclopsScannerModule.SaveData;

/// <summary>
/// Persisted scanner state for all Cyclops subs in the current save slot. Managed by Nautilus's
/// <see cref="SaveDataCache"/>: loaded/saved automatically alongside the game save, one file per
/// save slot, and keyed by each Cyclops's <see cref="PrefabIdentifier"/>.Id.
/// </summary>
[FileName("cyclops_scanner_state")]
public class ScannerSaveData : SaveDataCache
{
    public Dictionary<string, SubScannerState> Subs { get; set; } = new();
}

public class SubScannerState
{
    public string SelectedTechType { get; set; }
    public bool ScanActive { get; set; }
}
