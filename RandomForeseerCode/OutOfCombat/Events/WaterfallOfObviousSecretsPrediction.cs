using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Runs;
using RandomForeseer.Common;
using RandomForeseer.Common.HoverTips;

namespace RandomForeseer.OutOfCombat.Events;

internal static class WaterfallOfObviousSecretsPrediction
{
    public static IReadOnlyList<IHoverTip> GetHoverTips(WaterfallOfObviousSecrets waterfall, EventOption option)
    {
        var player = waterfall.Owner!;
        return option.TextKey switch
        {
            "WATERFALL_OF_OBVIOUS_SECRETS.pages.ENTER.options.BEAT_UP" =>
                OutOfCombatPredictionUtils.RelicTipsWithPickup(
                    player,
                    OutOfCombatPredictionUtils.PredictRelicRewards(player, 1, waterfall.Rng.Clone())),
            "WATERFALL_OF_OBVIOUS_SECRETS.pages.ENTER.options.NEGOTIATE" => PredictNegotiate(player),
            _ => []
        };
    }

    private static IReadOnlyList<IHoverTip> PredictNegotiate(Player player)
    {
        var options = new CardCreationOptions(
                [player.Character.CardPool],
                CardCreationSource.Other,
                CardRarityOddsType.Uniform,
                card => card.Rarity == CardRarity.Rare)
            .WithFlags(CardCreationFlags.NoUpgradeRoll);

        // Mirrors the event's direct CardFactory.CreateForReward call without adding IsCardReward.
        return [.. CardRewardPrediction.PredictCards(player, 1, options).ToPredictionHoverTips()];
    }
}
