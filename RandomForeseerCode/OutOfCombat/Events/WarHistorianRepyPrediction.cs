using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Events;
using RandomForeseer.RandomForeseerCode.Common;

namespace RandomForeseer.RandomForeseerCode.OutOfCombat.Events;

internal static class WarHistorianRepyPrediction
{
    public static IReadOnlyList<IHoverTip> GetHoverTips(WarHistorianRepy warHistorianRepy, EventOption option)
    {
        if (option.TextKey != "WAR_HISTORIAN_REPY.pages.INITIAL.options.UNLOCK_CHEST")
        {
            return [];
        }

        var player = warHistorianRepy.Owner!;
        var tips = PredictionHoverTips.Potions(PredictionUtils.PredictPotionRewards(
            player,
            2,
            player.PlayerRng.Rewards.Clone())).ToList();
        tips.AddRange(OutOfCombatPredictionUtils.RelicTipsWithPickup(player, OutOfCombatPredictionUtils.PredictRelicRewards(player, 2)));
        return tips;
    }
}
