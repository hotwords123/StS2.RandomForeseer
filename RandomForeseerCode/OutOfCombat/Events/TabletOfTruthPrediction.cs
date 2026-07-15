using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Events;
using RandomForeseer.RandomForeseerCode.Common;

namespace RandomForeseer.RandomForeseerCode.OutOfCombat.Events;

internal static class TabletOfTruthPrediction
{
    public static IReadOnlyList<IHoverTip> GetHoverTips(TabletOfTruth tabletOfTruth, EventOption option)
    {
        var upgradeCount = GetRandomUpgradeCountBeforeFinalAllUpgrade(option);
        return upgradeCount > 0
            ? PredictionHoverTips.Cards(OutOfCombatPredictionUtils.PredictUpgradedDeckCardsByNextItem(
                tabletOfTruth.Owner!,
                upgradeCount.Value,
                card => card.IsUpgradable,
                tabletOfTruth.Rng.Clone()))
            : [];
    }

    private static int? GetRandomUpgradeCountBeforeFinalAllUpgrade(EventOption option)
    {
        // Mirrors TabletOfTruth.LoseMaxHpAndUpgrade: decipher counts 0-3 pick one random card;
        // decipher count 4 upgrades all remaining cards, so it is intentionally excluded here.
        return option.TextKey switch
        {
            "TABLET_OF_TRUTH.pages.INITIAL.options.DECIPHER_1" => 4,
            "TABLET_OF_TRUTH.pages.DECIPHER_1.options.DECIPHER" => 3,
            "TABLET_OF_TRUTH.pages.DECIPHER_2.options.DECIPHER" => 2,
            "TABLET_OF_TRUTH.pages.DECIPHER_3.options.DECIPHER" => 1,
            _ => null
        };
    }
}
