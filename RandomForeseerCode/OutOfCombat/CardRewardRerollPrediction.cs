using HarmonyLib;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;
using RandomForeseer.RandomForeseerCode.Common.HoverTips;
using RandomForeseer.RandomForeseerCode.Data;

namespace RandomForeseer.RandomForeseerCode.OutOfCombat;

internal static class CardRewardRerollPrediction
{
    // A complete five-reward preview uses 129 generations; also bound larger reward pages.
    private const int MaxRewardGenerations = 256;

    private static readonly AccessTools.FieldRef<CardReward, Action?> AfterGeneratedField =
        AccessTools.FieldRefAccess<CardReward, Action?>("AfterGenerated");

    public static IReadOnlyList<IHoverTip> GetHoverTips(CardReward reward)
    {
        var settings = ModData.Settings;
        if (!settings.IsPredictionEnabled || !settings.DriftwoodRerollPredictionEnabled || !reward.CanReroll)
        {
            return [];
        }

        var otherRewards = RewardPagePredictionContext.GetPendingRerollRewards(reward)
            .Where(other => other != reward)
            .ToArray();
        var previewCount = Math.Min(settings.DriftwoodRerollPreviewCount, otherRewards.Length + 1);
        var results = PredictSequence(reward, otherRewards, previewCount);

        if (results is [var singleResult])
        {
            return [.. singleResult.Cards.ToPredictionHoverTips()];
        }

        return
        [
            PredictionHoverTipFactory.Text("driftwood_reroll_sequence"),
            .. results.Select((result, index) =>
                PredictionHoverTipFactory.CardBundle(result.Cards, isDimmed: index > 0))
        ];
    }

    private static IReadOnlyList<RerollResult> PredictSequence(
        CardReward reward,
        IReadOnlyList<CardReward> otherRewards,
        int previewCount)
    {
        List<RerollResult> results = [];
        List<RerollBranch> branches = [new(new RunPredictionContext(reward.Player), GroupRewards(otherRewards))];
        var depth = 0;
        var steps = 1;

        while (branches.Count > 0)
        {
            IReadOnlyList<CardModel>? agreedCards = null;
            foreach (var branch in branches)
            {
                var cards = PredictReward(branch.Context.Clone(), reward);

                if (agreedCards == null)
                {
                    agreedCards = cards;
                }
                else if (!agreedCards.Select(card => card.Id).SequenceEqual(cards.Select(card => card.Id)))
                {
                    // If any branch does not produce the same cards, return the results so far.
                    return results;
                }
            }

            results.Add(new RerollResult(agreedCards!));
            if (depth + 1 == previewCount)
            {
                break;
            }

            // Each new branch generates the other reward, then previews the current reward on a copy.
            steps += branches.Sum(branch => branch.RemainingGroups.Count) * 2;
            if (steps > MaxRewardGenerations)
            {
                break;
            }

            List<RerollBranch> nextBranches = [];
            foreach (var branch in branches)
            {
                foreach (var nextGroup in branch.RemainingGroups)
                {
                    var nextContext = branch.Context.Clone();
                    _ = PredictReward(nextContext, nextGroup.Reward);

                    nextBranches.Add(new RerollBranch(nextContext,
                    [
                        .. branch.RemainingGroups
                            .Select(group => group.Reward == nextGroup.Reward
                                ? group with { Count = group.Count - 1 }
                                : group)
                            .Where(group => group.Count > 0)
                    ]));
                }
            }

            branches = nextBranches;
            depth++;
        }

        return results;
    }

    private static IReadOnlyList<RerollGroup> GroupRewards(IReadOnlyList<CardReward> rewards)
    {
        List<RerollGroup> groups = [];
        foreach (var reward in rewards)
        {
            var index = groups.FindIndex(group => AreRerollEquivalent(group.Reward, reward));
            if (index < 0)
            {
                groups.Add(new RerollGroup(reward, 1));
            }
            else
            {
                var group = groups[index];
                groups[index] = group with { Count = group.Count + 1 };
            }
        }

        return groups;
    }

    private static bool AreRerollEquivalent(CardReward first, CardReward second)
    {
        var firstOptions = first.RerollOptions;
        var secondOptions = second.RerollOptions;

        // Preserve pool order and filter identity: creation hooks may expand or replace the original pools.
        return first.Player == second.Player &&
               first.OptionCount == second.OptionCount &&
               firstOptions.Source == secondOptions.Source &&
               firstOptions.RarityOdds == secondOptions.RarityOdds &&
               firstOptions.Flags == secondOptions.Flags &&
               ReferenceEquals(firstOptions.RngOverride, secondOptions.RngOverride) &&
               firstOptions.CardPools.SequenceEqual(secondOptions.CardPools) &&
               firstOptions.CardPoolFilter == secondOptions.CardPoolFilter &&
               AfterGeneratedField(first) == AfterGeneratedField(second);
    }

    private static IReadOnlyList<CardModel> PredictReward(RunPredictionContext context, CardReward reward)
    {
        return CardRewardPrediction.PredictCards(
            context,
            reward.OptionCount,
            CardRewardPrediction.CloneOptions(reward.RerollOptions, context.GetOrCloneRng),
            AfterGeneratedField(reward));
    }

    private readonly record struct RerollResult(IReadOnlyList<CardModel> Cards);

    private readonly record struct RerollGroup(CardReward Reward, int Count);

    private readonly record struct RerollBranch(
        RunPredictionContext Context,
        IReadOnlyList<RerollGroup> RemainingGroups);
}
