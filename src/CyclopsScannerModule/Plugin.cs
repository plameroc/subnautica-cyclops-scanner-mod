using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using CyclopsScannerModule.Items;
using HarmonyLib;
using UnityEngine;

namespace CyclopsScannerModule;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
[BepInDependency("com.snmodding.nautilus")]
public class Plugin : BaseUnityPlugin
{
    public new static ManualLogSource Logger { get; private set; }

    internal static ConfigEntry<KeyCode> MenuKey;

    private static Assembly Assembly { get; } = Assembly.GetExecutingAssembly();

    private void Awake()
    {
        // set project-scoped logger instance
        Logger = base.Logger;

        MenuKey = Config.Bind("Input", "OpenScannerMenu", KeyCode.K, "Opens the Cyclops scanner resource selection menu while inside a Cyclops with a Cyclops Scanner Module installed.");

        // register custom items
        ScannerModuleItem.Register();

        // Menu host component; lives on BepInEx's DontDestroyOnLoad manager object.
        gameObject.AddComponent<UI.ScannerMenu>();

        // register harmony patches, if there are any
        Harmony.CreateAndPatchAll(Assembly, $"{PluginInfo.PLUGIN_GUID}");
        Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
    }
}