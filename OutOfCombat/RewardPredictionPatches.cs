using HarmonyLib;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Rewards;
using RandomForeseer.Common;

namespace RandomForeseer.OutOfCombat;

[HarmonyPatch(typeof(Reward), nameof(Reward.HoverTips), MethodType.Getter)]
internal static class RewardPredictionPatch
{
    private static void Postfix(Reward __instance, ref IEnumerable<IHoverTip> __result)
    {
        var predictionTips = __instance switch
        {
            RelicReward { Relic: { } relic } relicReward =>
                RelicPickupPrediction.GetHoverTips(relicReward.Player, relic),

            PotionReward { Potion: { } potion } potionReward =>
                PotionPrediction.GetHoverTips(potionReward.Player, potion),

            _ => []
        };

        if (predictionTips.Count > 0)
        {
            __result = __result.Concat(predictionTips).ToList();
        }
    }
}
