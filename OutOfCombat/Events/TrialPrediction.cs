using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Runs;
using RandomForeseer.Common;

namespace RandomForeseer.OutOfCombat.Events;

internal static class TrialPrediction
{
    public static void Register()
    {
        EventOptionPredictionRegistry.Register<Trial>(GetHoverTips);
    }

    private static IReadOnlyList<IHoverTip> GetHoverTips(Trial trial, EventOption option)
    {
        var player = trial.Owner!;
        return option.TextKey switch
        {
            "TRIAL.pages.MERCHANT.options.GUILTY" =>
                OutOfCombatPredictionUtils.RelicTipsWithPickup(player, OutOfCombatPredictionUtils.PredictRelicRewards(player, 2)),
            "TRIAL.pages.NONDESCRIPT.options.GUILTY" =>
                PredictionHoverTips.CardBundles(OutOfCombatPredictionUtils.PredictCardRewardBundles(
                    player,
                    2,
                    3,
                    CardCreationOptions.ForNonCombatWithDefaultOdds([player.Character.CardPool]))),
            "TRIAL.pages.NONDESCRIPT.options.INNOCENT" =>
                PredictionHoverTips.CardBundles(PredictNondescriptInnocent(player)),
            _ => []
        };
    }

    private static IReadOnlyList<IReadOnlyList<CardModel>> PredictNondescriptInnocent(Player player)
    {
        // The real event adds Doubt to the deck before opening the transform selector.
        var addedCurse = PredictionUtils.CreatePreviewCard(ModelDb.Card<Doubt>(), player);
        return OutOfCombatPredictionUtils.PredictDistinctDeckTransformResultBundles(
            player,
            player.RunState.Rng.Niche,
            transformCount: 2,
            extraTransformableCards: [addedCurse]);
    }
}
