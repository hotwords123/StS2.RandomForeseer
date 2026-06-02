using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Events;

namespace RandomForeseer.OutOfCombat.Events;

internal static class LuminousChoirPrediction
{
    public static void Register()
    {
        EventOptionPredictionRegistry.Register<LuminousChoir>(GetHoverTips);
    }

    private static IReadOnlyList<IHoverTip> GetHoverTips(LuminousChoir luminousChoir, EventOption option)
    {
        return option.TextKey == "LUMINOUS_CHOIR.pages.INITIAL.options.OFFER_TRIBUTE"
            ? OutOfCombatPredictionUtils.RelicTipsWithPickup(
                luminousChoir.Owner!,
                OutOfCombatPredictionUtils.PredictRelicRewards(luminousChoir.Owner!, 1))
            : [];
    }
}
