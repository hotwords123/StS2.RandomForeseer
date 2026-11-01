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
        List<RerollBranch> branches = [new(new RunPredictionContext(reward.Player), otherRewards)];
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
            steps += branches.Count * (otherRewards.Count - depth) * 2;
            if (steps > MaxRewardGenerations)
            {
                break;
            }

            List<RerollBranch> nextBranches = [];
            foreach (var branch in branches)
            {
                foreach (var nextReward in branch.RemainingRewards)
                {
                    var nextContext = branch.Context.Clone();
                    _ = PredictReward(nextContext, nextReward);

                    nextBranches.Add(new RerollBranch(
                        nextContext,
                        [.. branch.RemainingRewards.Where(other => other != nextReward)]));
                }
            }

            branches = nextBranches;
            depth++;
        }

        return results;
    }

    private static IReadOnlyList<CardModel> PredictReward(RunPredictionContext context, CardReward reward)
    {
        return CardRewardPrediction.PredictCards(
            context,
            reward.OptionCount,
            CardRewardPrediction.CloneOptions(reward.RerollOptions, context.GetOrCloneRng),
            AfterGeneratedField(reward));
    }

    private sealed record RerollResult(IReadOnlyList<CardModel> Cards);

    private sealed record RerollBranch(
        RunPredictionContext Context,
        IReadOnlyList<CardReward> RemainingRewards);
}
