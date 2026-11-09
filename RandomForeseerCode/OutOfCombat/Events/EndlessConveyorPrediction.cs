using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Runs;
using RandomForeseer.Common;
using RandomForeseer.Common.HoverTips;

namespace RandomForeseer.OutOfCombat.Events;

internal static class EndlessConveyorPrediction
{
    public static IReadOnlyList<IHoverTip> GetHoverTips(EndlessConveyor endlessConveyor, EventOption option)
    {
        var player = endlessConveyor.Owner!;
        return option.TextKey switch
        {
            "ENDLESS_CONVEYOR.pages.INITIAL.options.OBSERVE_CHEF" =>
                [.. OutOfCombatPredictionUtils.PredictUpgradedDeckCardsByNextItem(
                    player,
                    1,
                    card => card.IsUpgradable,
                    endlessConveyor.Rng.Clone()).ToPredictionHoverTips()],
            "ENDLESS_CONVEYOR.pages.ALL.options.FRIED_EEL" =>
                [.. CardRewardPrediction.PredictCards(
                    player,
                    1,
                    CardCreationOptions.ForNonCombatWithDefaultOdds([ModelDb.CardPool<ColorlessCardPool>()])).ToPredictionHoverTips()],
            "ENDLESS_CONVEYOR.pages.ALL.options.JELLY_LIVER" =>
                [.. PredictJellyLiver(endlessConveyor).ToPredictionHoverTips()],
            "ENDLESS_CONVEYOR.pages.ALL.options.SUSPICIOUS_CONDIMENT" =>
                [.. OutOfCombatPredictionUtils.PredictUniformPotions(player, 1).ToPredictionHoverTips()],
            "ENDLESS_CONVEYOR.pages.ALL.options.SPICY_SNAPPY" =>
                [.. OutOfCombatPredictionUtils.PredictUpgradedDeckCardsByNextItem(
                    player,
                    1,
                    card => card.IsUpgradable,
                    endlessConveyor.Rng.Clone()).ToPredictionHoverTips()],
            _ => []
        };
    }

    private static IReadOnlyList<CardModel> PredictJellyLiver(EndlessConveyor endlessConveyor)
    {
        return OutOfCombatPredictionUtils.PredictDistinctDeckTransformResults(endlessConveyor.Owner!, endlessConveyor.Rng);
    }
}
