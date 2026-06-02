using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Events;
using RandomForeseer.Common;

namespace RandomForeseer.OutOfCombat.Events;

internal static class WelcomeToWongosPrediction
{
    public static void Register()
    {
        EventOptionPredictionRegistry.Register<WelcomeToWongos>(GetHoverTips);
    }

    private static IReadOnlyList<IHoverTip> GetHoverTips(WelcomeToWongos welcomeToWongos, EventOption option)
    {
        var player = welcomeToWongos.Owner!;
        return option.TextKey switch
        {
            "WELCOME_TO_WONGOS.pages.INITIAL.options.BARGAIN_BIN" =>
                OutOfCombatPredictionUtils.RelicTipsWithPickup(player, OutOfCombatPredictionUtils.PredictRelicRewards(player, [RelicRarity.Common], relic => relic.IsAllowedInShops)),
            "WELCOME_TO_WONGOS.pages.INITIAL.options.LEAVE" =>
                PredictionHoverTips.Cards(OutOfCombatPredictionUtils.PredictDowngradedDeckCardsByNextItem(player, 1, card => card.IsUpgraded, welcomeToWongos.Rng)),
            _ => []
        };
    }
}
