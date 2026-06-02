using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Events;
using RandomForeseer.Common;

namespace RandomForeseer.OutOfCombat.Events;

internal static class TabletOfTruthPrediction
{
    public static void Register()
    {
        EventOptionPredictionRegistry.Register<TabletOfTruth>(GetHoverTips);
    }

    private static IReadOnlyList<IHoverTip> GetHoverTips(TabletOfTruth tabletOfTruth, EventOption option)
    {
        return option.TextKey is
            "TABLET_OF_TRUTH.pages.INITIAL.options.DECIPHER_1" or
            "TABLET_OF_TRUTH.pages.DECIPHER_1.options.DECIPHER" or
            "TABLET_OF_TRUTH.pages.DECIPHER_2.options.DECIPHER" or
            "TABLET_OF_TRUTH.pages.DECIPHER_3.options.DECIPHER"
            ? PredictionHoverTips.Cards(OutOfCombatPredictionUtils.PredictUpgradedDeckCardsByNextItem(tabletOfTruth.Owner!, 1, card => card.IsUpgradable, tabletOfTruth.Rng))
            : [];
    }
}
