using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Rewards;
using RandomForeseer.RandomForeseerCode.Common;

namespace RandomForeseer.RandomForeseerCode.OutOfCombat;

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

internal static class RewardPagePredictionContext
{
    private static readonly ConditionalWeakTable<RelicModel, RewardsSetReference> RewardsSets = [];

    public static void Register(RewardsSet set)
    {
        foreach (var reward in FlattenRewards(set.Rewards).OfType<RelicReward>())
        {
            if (reward.Relic is { } relic)
            {
                RewardsSets.AddOrUpdate(relic, new RewardsSetReference(set));
            }
        }
    }

    public static bool HasOtherPendingReward(RelicModel relic)
    {
        return RewardsSets.TryGetValue(relic, out var reference) &&
            FlattenRewards(reference.Set.Rewards)
                .Any(other => !other.SuccessfullySelected &&
                    !(other is RelicReward relicReward && ReferenceEquals(relicReward.Relic, relic)));
    }

    private static IEnumerable<Reward> FlattenRewards(IEnumerable<Reward> rewards)
    {
        foreach (var reward in rewards)
        {
            yield return reward;
            if (reward is not LinkedRewardSet linkedReward)
            {
                continue;
            }

            foreach (var childReward in FlattenRewards(linkedReward.Rewards))
            {
                yield return childReward;
            }
        }
    }

    private sealed class RewardsSetReference(RewardsSet set)
    {
        public RewardsSet Set { get; } = set;
    }
}
