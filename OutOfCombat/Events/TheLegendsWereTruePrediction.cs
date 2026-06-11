using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Events;
using RandomForeseer.Common;

namespace RandomForeseer.OutOfCombat.Events;

internal static class TheLegendsWereTruePrediction
{
    public static IReadOnlyList<IHoverTip> GetHoverTips(TheLegendsWereTrue legends, EventOption option)
    {
        return option.TextKey == "THE_LEGENDS_WERE_TRUE.pages.INITIAL.options.SLOWLY_FIND_AN_EXIT"
            ? PredictionHoverTips.Potions(OutOfCombatPredictionUtils.PredictUniformPotions(legends.Owner!, 1))
            : [];
    }
}
