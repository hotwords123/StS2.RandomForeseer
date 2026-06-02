using HarmonyLib;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Runs;
using RandomForeseer.Common;

namespace RandomForeseer.OutOfCombat;

internal static class CardRewardRerollPrediction
{
    private static readonly AccessTools.FieldRef<CardReward, int> OptionCountField =
        AccessTools.FieldRefAccess<CardReward, int>("<OptionCount>k__BackingField");

    private static readonly AccessTools.FieldRef<CardReward, CardCreationOptions> RerollOptionsField =
        AccessTools.FieldRefAccess<CardReward, CardCreationOptions>("<RerollOptions>k__BackingField");

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
            OptionCountField(reward),
            RerollOptionsField(reward),
            PredictionUtils.CloneRng(player.PlayerRng.Rewards),
            PredictionUtils.CloneRng(player.RunState.Rng.Niche),
            AfterGeneratedField(reward));

        return PredictionHoverTips.Cards(cards);
    }
}
