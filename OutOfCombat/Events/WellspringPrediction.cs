using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Events;
using RandomForeseer.Common;

namespace RandomForeseer.OutOfCombat.Events;

internal static class WellspringPrediction
{
    public static void Register()
    {
        EventOptionPredictionRegistry.Register<Wellspring>(GetHoverTips);
    }

    private static IReadOnlyList<IHoverTip> GetHoverTips(Wellspring wellspring, EventOption option)
    {
        return option.TextKey == "WELLSPRING.pages.INITIAL.options.BOTTLE"
            ? PredictionHoverTips.Potions(OutOfCombatPredictionUtils.PredictUniformPotions(
                wellspring.Owner!,
                1,
                wellspring.Owner!.PlayerRng.Rewards))
            : [];
    }
}
