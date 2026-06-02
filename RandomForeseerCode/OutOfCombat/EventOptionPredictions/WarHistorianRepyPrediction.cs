using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Events;

namespace RandomForeseer;

internal static class WarHistorianRepyPrediction
{
    public static void Register()
    {
        EventOptionPredictionRegistry.Register<WarHistorianRepy>(GetHoverTips);
    }

    private static IReadOnlyList<IHoverTip> GetHoverTips(WarHistorianRepy warHistorianRepy, EventOption option)
    {
        if (option.TextKey != "WAR_HISTORIAN_REPY.pages.INITIAL.options.UNLOCK_CHEST")
        {
            return [];
        }

        var player = warHistorianRepy.Owner!;
        var tips = PredictionHoverTips.Potions(OutOfCombatPredictionUtils.PredictPotionRewards(player, 2, player.PlayerRng.Rewards)).ToList();
        tips.AddRange(OutOfCombatPredictionUtils.RelicTipsWithPickup(player, OutOfCombatPredictionUtils.PredictRelicRewards(player, 2)));
        return tips;
    }
}
