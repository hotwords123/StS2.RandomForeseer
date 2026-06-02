using HarmonyLib;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Rewards;

namespace RandomForeseer.OutOfCombat;

[HarmonyPatch(typeof(Reward), nameof(Reward.HoverTips), MethodType.Getter)]
internal static class RewardRelicPredictionPatch
{
    private static void Postfix(Reward __instance, ref IEnumerable<IHoverTip> __result)
    {
        if (__instance is not RelicReward { Relic: { } relic } relicReward)
        {
            return;
        }

        var predictionTips = OutOfCombatRelicPrediction.GetHoverTips(relicReward.Player, relic);
        if (predictionTips.Count > 0)
        {
            __result = __result.Concat(predictionTips).ToList();
        }
    }
}
