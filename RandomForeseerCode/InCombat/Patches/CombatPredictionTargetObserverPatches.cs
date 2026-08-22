using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace RandomForeseer.RandomForeseerCode.InCombat.Patches;

[HarmonyPatch(typeof(NTargetManager))]
internal static class NTargetManagerPatches
{
    [HarmonyPatch(nameof(NTargetManager.FinishTargeting))]
    [HarmonyPrefix]
    private static void OnTargetingFinishing(NTargetManager __instance)
    {
        CombatPredictionTargetObserver.OnTargetingFinishing(__instance);
    }
}
