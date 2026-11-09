using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;

namespace RandomForeseer.OutOfCombat;

/// <summary>Tracks the reward page that owns each relic or card reward used by prediction.</summary>
internal static class RewardPagePredictionContext
{
    private static readonly ConditionalWeakTable<RelicModel, RewardsSet> RewardsSets = [];
    private static readonly ConditionalWeakTable<CardReward, RewardsSet> CardRewardSets = [];

    public static void Register(RewardsSet rewardsSet)
    {
        foreach (var reward in FlattenRewards(rewardsSet.Rewards))
        {
            switch (reward)
            {
                case RelicReward { Relic: { } relic }:
                    RewardsSets.AddOrUpdate(relic, rewardsSet);
                    break;

                case CardReward cardReward:
                    CardRewardSets.AddOrUpdate(cardReward, rewardsSet);
                    break;
            }
        }
    }

    /// <summary>
    /// Returns a current snapshot of the unclaimed, rerollable card rewards on the registered page,
    /// including <paramref name="reward"/> when eligible. Returns an empty list if no page is registered.
    /// </summary>
    public static IReadOnlyList<CardReward> GetPendingRerollRewards(CardReward reward)
    {
        if (!CardRewardSets.TryGetValue(reward, out var rewardsSet))
        {
            return [];
        }

        return
        [
            .. FlattenRewards(rewardsSet.Rewards)
                .OfType<CardReward>()
                .Where(other => !other.SuccessfullySelected && other.CanReroll)
        ];
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
