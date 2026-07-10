using CyclopsScannerModule.Behaviours;
using HarmonyLib;

namespace CyclopsScannerModule.Patches;

[HarmonyPatch(typeof(SubRoot), "Start")]
internal static class SubRoot_Start_Patch
{
    private static void Postfix(SubRoot __instance)
    {
        if (!__instance.isCyclops) return;
        if (__instance.GetComponent<CyclopsScannerController>() == null)
        {
            __instance.gameObject.AddComponent<CyclopsScannerController>();
        }
    }
}
