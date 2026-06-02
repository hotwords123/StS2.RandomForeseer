using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Events;
using RandomForeseer.Common;

namespace RandomForeseer.OutOfCombat.Events;

internal static class DoorsOfLightAndDarkPrediction
{
    public static void Register()
    {
        EventOptionPredictionRegistry.Register<DoorsOfLightAndDark>(GetHoverTips);
    }

    private static IReadOnlyList<IHoverTip> GetHoverTips(DoorsOfLightAndDark doors, EventOption option)
    {
        return option.TextKey == "DOORS_OF_LIGHT_AND_DARK.pages.INITIAL.options.LIGHT"
            ? PredictionHoverTips.Cards(OutOfCombatPredictionUtils.PredictUpgradedDeckCards(doors.Owner!, 2, card => card.IsUpgradable))
            : [];
    }
}
