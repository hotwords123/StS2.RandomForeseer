using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;

namespace RandomForeseer.RandomForeseerCode.InCombat.Patches;

[HarmonyPatch(typeof(NHandCardHolder))]
internal static class CombatPredictionCardHighlightPatches
{
    [HarmonyPatch(nameof(NHandCardHolder.UpdateCard))]
    [HarmonyPostfix]
    private static void ShowHighlightAfterCardUpdate(NHandCardHolder __instance)
    {
        CombatPredictionCardHighlight.ApplyHighlightToHolder(__instance);
    }
}
