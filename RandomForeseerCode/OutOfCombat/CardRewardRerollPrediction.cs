using HarmonyLib;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Rewards;
using RandomForeseer.RandomForeseerCode.Common.HoverTips;
using RandomForeseer.RandomForeseerCode.Data;

namespace RandomForeseer.RandomForeseerCode.OutOfCombat;

internal static class CardRewardRerollPrediction
{
    private static readonly AccessTools.FieldRef<CardReward, Action?> AfterGeneratedField =
        AccessTools.FieldRefAccess<CardReward, Action?>("AfterGenerated");

    public static IReadOnlyList<IHoverTip> GetHoverTips(CardReward reward)
    {
        var settings = ModData.Settings;
        if (!settings.IsPredictionEnabled || !settings.DriftwoodRerollPredictionEnabled || !reward.CanReroll)
        {
            return [];
        }

        var context = new RunPredictionContext(reward.Player);
        var cards = CardRewardPrediction.PredictCards(
            context,
            reward.OptionCount,
            CardRewardPrediction.CloneOptions(reward.RerollOptions, context.GetOrCloneRng),
            AfterGeneratedField(reward));

        return [.. cards.ToPredictionHoverTips()];
    }
}
