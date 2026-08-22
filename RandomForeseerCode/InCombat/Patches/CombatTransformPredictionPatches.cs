using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using RandomForeseer.RandomForeseerCode.Common.HoverTips;

namespace RandomForeseer.RandomForeseerCode.InCombat.Patches;

[HarmonyPatch(typeof(NPlayerHand))]
internal static class CombatTransformPredictionPlayerHandPatch
{
    [HarmonyPatch(nameof(NPlayerHand.SelectCards))]
    [HarmonyPrefix]
    private static void BeginSession(NPlayerHand __instance, AbstractModel? source)
    {
        CombatTransformPrediction.BeginSession(__instance, source);
    }

    [HarmonyPatch(nameof(NPlayerHand.AfterCardsSelected))]
    [HarmonyPrefix]
    private static void EndSession(NPlayerHand __instance)
    {
        CombatTransformPrediction.EndSession(__instance);
    }

    [HarmonyPatch(nameof(NPlayerHand._ExitTree))]
    [HarmonyPrefix]
    private static void CleanupSession(NPlayerHand __instance)
    {
        CombatTransformPrediction.EndSession(__instance);
    }
}

[HarmonyPatch(typeof(NSelectedHandCardHolder), "CreateHoverTips")]
internal static class CombatTransformPredictionSelectedHoverTipsPatch
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(NSelectedHandCardHolder __instance)
    {
        PredictionHoverTipSetHelper.EnsureHoverTipSet(__instance)?.SetAlignmentForCardHolder(__instance);
    }
}
