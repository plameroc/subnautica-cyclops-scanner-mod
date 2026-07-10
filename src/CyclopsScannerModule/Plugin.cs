using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using CyclopsScannerModule.Behaviours;
using CyclopsScannerModule.Items;
using CyclopsScannerModule.SaveData;
using HarmonyLib;
using Nautilus.Handlers;
using Nautilus.Utility;
using UnityEngine;

namespace CyclopsScannerModule;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
[BepInDependency("com.snmodding.nautilus")]
public class Plugin : BaseUnityPlugin
{
    public new static ManualLogSource Logger { get; private set; }

    internal static ConfigEntry<KeyCode> MenuKey;
    internal static ScannerSaveData SaveState;

    private static Assembly Assembly { get; } = Assembly.GetExecutingAssembly();

    private void Awake()
    {
        // set project-scoped logger instance
        Logger = base.Logger;

        MenuKey = Config.Bind("Input", "OpenScannerMenu", KeyCode.K, "Opens the Cyclops scanner resource selection menu while inside a Cyclops with a Cyclops Scanner Module installed.");

        // register per-save scanner state cache
        SaveState = SaveDataHandler.RegisterSaveDataCache<ScannerSaveData>();
        SaveState.OnStartedSaving += (sender, e) =>
        {
            foreach (var controller in CyclopsScannerController.All)
            {
                if (controller != null)
                    controller.WriteToSave();
            }
        };
        // Prevents in-memory entries from a previously played save slot leaking into the next
        // slot loaded in the same game session.
        SaveState.OnStartedLoading += (sender, e) => SaveState.Subs.Clear();
#pragma warning disable CS0618 // RegisterOnFinishLoadingEvent is obsolete in favor of WaitScreenHandler.RegisterLateLoadTask(); spec requires this exact API.
        SaveUtils.RegisterOnFinishLoadingEvent(() =>
        {
            // Re-apply pass: controller Start may have run before the save file was loaded.
            foreach (var controller in CyclopsScannerController.All)
            {
                if (controller != null)
                    controller.RestoreFromSave();
            }
        });
#pragma warning restore CS0618

        // register custom items
        ScannerModuleItem.Register();

        // Menu host component; lives on BepInEx's DontDestroyOnLoad manager object.
        gameObject.AddComponent<UI.ScannerMenu>();

        // register harmony patches, if there are any
        Harmony.CreateAndPatchAll(Assembly, $"{PluginInfo.PLUGIN_GUID}");
        Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
    }
}