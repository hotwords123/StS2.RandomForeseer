using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Runs;
using RandomForeseer.Common;

namespace RandomForeseer.OutOfCombat.Events;

internal static class EndlessConveyorPrediction
{
    public static IReadOnlyList<IHoverTip> GetHoverTips(EndlessConveyor endlessConveyor, EventOption option)
    {
        var player = endlessConveyor.Owner!;
        return option.TextKey switch
        {
            "ENDLESS_CONVEYOR.pages.INITIAL.options.OBSERVE_CHEF" =>
                PredictionHoverTips.Cards(OutOfCombatPredictionUtils.PredictUpgradedDeckCardsByNextItem(
                    player,
                    1,
                    card => card.IsUpgradable,
                    PredictionUtils.CloneRng(endlessConveyor.Rng))),
            "ENDLESS_CONVEYOR.pages.ALL.options.FRIED_EEL" =>
                PredictionHoverTips.Cards(OutOfCombatPredictionUtils.PredictCards(
                    player,
                    1,
                    CardCreationOptions.ForNonCombatWithDefaultOdds([ModelDb.CardPool<ColorlessCardPool>()]))),
            "ENDLESS_CONVEYOR.pages.ALL.options.JELLY_LIVER" =>
                PredictionHoverTips.Cards(PredictJellyLiver(endlessConveyor)),
            "ENDLESS_CONVEYOR.pages.ALL.options.SUSPICIOUS_CONDIMENT" =>
                PredictionHoverTips.Potions(OutOfCombatPredictionUtils.PredictUniformPotions(player, 1)),
            "ENDLESS_CONVEYOR.pages.ALL.options.SPICY_SNAPPY" =>
                PredictionHoverTips.Cards(OutOfCombatPredictionUtils.PredictUpgradedDeckCardsByNextItem(
                    player,
                    1,
                    card => card.IsUpgradable,
                    PredictionUtils.CloneRng(endlessConveyor.Rng))),
            _ => []
        };
    }

    private static IReadOnlyList<CardModel> PredictJellyLiver(EndlessConveyor endlessConveyor)
    {
        return OutOfCombatPredictionUtils.PredictDistinctDeckTransformResults(endlessConveyor.Owner!, endlessConveyor.Rng);
    }
}
