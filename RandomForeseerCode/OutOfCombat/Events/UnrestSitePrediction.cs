using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Events;

namespace RandomForeseer.RandomForeseerCode.OutOfCombat.Events;

internal static class UnrestSitePrediction
{
    public static IReadOnlyList<IHoverTip> GetHoverTips(UnrestSite unrestSite, EventOption option)
    {
        return option.TextKey == "UNREST_SITE.pages.INITIAL.options.KILL"
            ? OutOfCombatPredictionUtils.RelicTipsWithPickup(
                unrestSite.Owner!,
                OutOfCombatPredictionUtils.PredictRelicRewards(unrestSite.Owner!, 1))
            : [];
    }
}
