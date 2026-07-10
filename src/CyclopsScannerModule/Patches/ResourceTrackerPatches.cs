using CyclopsScannerModule.Behaviours;
using HarmonyLib;
using UnityEngine;

namespace CyclopsScannerModule.Patches;

[HarmonyPatch(typeof(uGUI_ResourceTracker), "GatherScanned")]
internal static class UGUIResourceTracker_GatherScanned_Patch
{
    private const float RoomGatherRange = 500f; // mirrors vanilla's scanner-room gather radius

    private static void Postfix(uGUI_ResourceTracker __instance)
    {
        // Vanilla shows only the drone's own room while a camera-drone screen is active.
        if (uGUI_CameraDrone.main != null && uGUI_CameraDrone.main.GetScreen() != null)
            return;

        var cam = MainCamera.camera;
        if (cam == null)
            return;

        Vector3 camPos = cam.transform.position;
        for (int i = 0; i < CyclopsScannerController.All.Count; i++)
        {
            var controller = CyclopsScannerController.All[i];
            if (controller == null || !controller.IsOperational)
                continue;
            Vector3 subPos = controller.transform.position;
            if ((subPos - camPos).sqrMagnitude > RoomGatherRange * RoomGatherRange)
                continue;
            ResourceTrackerDatabase.GetNodes(subPos, CyclopsScannerController.ScanRange, controller.SelectedType, __instance.nodes);
        }
    }
}
