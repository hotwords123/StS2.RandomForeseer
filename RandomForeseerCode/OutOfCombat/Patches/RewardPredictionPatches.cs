using HarmonyLib;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Rewards;
using RandomForeseer.RandomForeseerCode.Common;

namespace RandomForeseer.RandomForeseerCode.OutOfCombat.Patches;

[HarmonyPatch(typeof(Reward), nameof(Reward.HoverTips), MethodType.Getter)]
internal static class RewardPredictionPatch
{
    private static void Postfix(Reward __instance, ref IEnumerable<IHoverTip> __result)
    {
        var predictionTips = __instance switch
        {
            RelicReward { Relic: { } relic, Player: var player } =>
                RelicPickupPrediction.GetHoverTips(player, relic),

            PotionReward { Potion: { } potion, Player: var player } =>
                PotionPrediction.GetHoverTips(player, potion),

            _ => []
        };

        if (predictionTips.Count > 0)
        {
            __result = __result.Concat(predictionTips);
        }
    }
}

[HarmonyPatch(typeof(NRewardsScreen), nameof(NRewardsScreen.ShowScreen))]
internal static class RewardScreenPredictionContextPatch
{
    private static void Postfix(RewardsSet set)
    {
        RewardPagePredictionContext.Register(set);
    }
}
