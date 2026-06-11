using HarmonyLib;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Rewards;
using RandomForeseer.Common;

namespace RandomForeseer.OutOfCombat;

internal static class CardRewardRerollPrediction
{
    private static readonly AccessTools.FieldRef<CardReward, Action?> AfterGeneratedField =
        AccessTools.FieldRefAccess<CardReward, Action?>("AfterGenerated");

    public static IReadOnlyList<IHoverTip> GetHoverTips(CardReward reward)
    {
        if (!RandomForeseerSettings.IsPredictionFeatureEnabled(RandomForeseerSettings.EnableDriftwoodRerollPrediction) ||
            !reward.CanReroll)
        {
            return [];
        }

        var player = reward.Player;
        var cards = CardRewardPrediction.PredictCards(
            player,
            reward.OptionCount,
            reward.RerollOptions,
            AfterGeneratedField(reward));

        return PredictionHoverTips.Cards(cards);
    }
}
