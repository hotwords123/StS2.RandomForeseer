using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Potions;
using MegaCrit.Sts2.Core.Runs;
using RandomForeseer.RandomForeseerCode.InCombat;

namespace RandomForeseer.RandomForeseerCode.Common;

[HarmonyPatch(typeof(PotionModel), nameof(PotionModel.HoverTips), MethodType.Getter)]
internal static class PotionPredictionHoverTipsPatch
{
    private static void Postfix(PotionModel __instance, ref IEnumerable<IHoverTip> __result)
    {
        if (!__instance.IsMutable || __instance.Owner == null || __instance.Owner.RunState is not RunState)
        {
            return;
        }

        var predictionTips = PotionPrediction.GetHoverTips(__instance);
        if (predictionTips.Count > 0)
        {
            __result = __result.Concat(predictionTips);
        }
    }
}

[HarmonyPatch(typeof(NPotionHolder), "TargetNode", [typeof(TargetType)])]
internal static class PotionTargetPredictionPatch
{
    private static void Prefix(NPotionHolder __instance, TargetType targetType, out long __state)
    {
        __state = PotionTargetPredictionController.Begin(__instance, targetType);
    }

    private static void Postfix(NPotionHolder __instance, long __state, ref Task __result)
    {
        __result = PotionTargetPredictionController.CleanupAfterCompletion(__instance, __state, __result);
    }
}
