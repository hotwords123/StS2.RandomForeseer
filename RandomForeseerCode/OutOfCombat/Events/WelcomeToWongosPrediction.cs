using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Events;
using RandomForeseer.RandomForeseerCode.Common;

namespace RandomForeseer.RandomForeseerCode.OutOfCombat.Events;

internal static class WelcomeToWongosPrediction
{
    public static IReadOnlyList<IHoverTip> GetHoverTips(WelcomeToWongos welcomeToWongos, EventOption option)
    {
        var player = welcomeToWongos.Owner!;
        return option.TextKey switch
        {
            "WELCOME_TO_WONGOS.pages.INITIAL.options.BARGAIN_BIN" =>
                OutOfCombatPredictionUtils.RelicTipsWithPickup(player, OutOfCombatPredictionUtils.PredictRelicRewards(player, [RelicRarity.Common], relic => relic.IsAllowedInShops)),
            "WELCOME_TO_WONGOS.pages.INITIAL.options.LEAVE" =>
                PredictionHoverTips.Cards(OutOfCombatPredictionUtils.PredictDowngradedDeckCardsByNextItem(
                    player,
                    1,
                    card => card.IsUpgraded,
                    welcomeToWongos.Rng.Clone())),
            _ => []
        };
    }
}
