using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Events;

namespace RandomForeseer.OutOfCombat.Events;

internal static class UnrestSitePrediction
{
    public static void Register()
    {
        EventOptionPredictionRegistry.Register<UnrestSite>(GetHoverTips);
    }

    private static IReadOnlyList<IHoverTip> GetHoverTips(UnrestSite unrestSite, EventOption option)
    {
        return OutOfCombatPredictionUtils.PredictRelicsWithPickup(
            unrestSite.Owner!,
            option,
            OutOfCombatPredictionUtils.PredictRelicRewards(unrestSite.Owner!, 1));
    }
}
