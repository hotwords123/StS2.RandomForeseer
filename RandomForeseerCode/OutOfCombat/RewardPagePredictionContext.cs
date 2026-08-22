using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;

namespace RandomForeseer.RandomForeseerCode.OutOfCombat;

/// <summary>Tracks the reward page that owns each relic used by pickup prediction.</summary>
internal static class RewardPagePredictionContext
{
    private static readonly ConditionalWeakTable<RelicModel, RewardsSet> RewardsSets = [];

    public static void Register(RewardsSet rewardsSet)
    {
        foreach (var reward in FlattenRewards(rewardsSet.Rewards).OfType<RelicReward>())
        {
            if (reward.Relic is { } relic)
            {
                RewardsSets.AddOrUpdate(relic, rewardsSet);
            }
        }
    }

    public static bool HasOtherPendingReward(RelicModel relic)
    {
        if (!RewardsSets.TryGetValue(relic, out var rewardsSet))
        {
            return false;
        }

        return FlattenRewards(rewardsSet.Rewards).Any(other =>
            !other.SuccessfullySelected &&
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
}
