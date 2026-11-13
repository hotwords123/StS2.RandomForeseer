using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Events;
using RandomForeseer.Common;

namespace RandomForeseer.OutOfCombat.Events;

internal static class LuminousChoirPrediction
{
    public static IReadOnlyList<IHoverTip> GetHoverTips(LuminousChoir luminousChoir, EventOption option)
    {
        return option.TextKey == "LUMINOUS_CHOIR.pages.INITIAL.options.OFFER_TRIBUTE"
            ? OutOfCombatPredictionUtils.RelicTipsWithPickup(
                luminousChoir.Owner!,
                OutOfCombatPredictionUtils.PredictRelicRewards(luminousChoir.Owner!, 1, luminousChoir.Rng.Clone()))
            : [];
    }
}
