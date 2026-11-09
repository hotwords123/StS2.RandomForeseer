using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Events;
using RandomForeseer.Common;
using RandomForeseer.Common.HoverTips;

namespace RandomForeseer.OutOfCombat.Events;

internal static class WarHistorianRepyPrediction
{
    public static IReadOnlyList<IHoverTip> GetHoverTips(WarHistorianRepy warHistorianRepy, EventOption option)
    {
        if (option.TextKey != "WAR_HISTORIAN_REPY.pages.INITIAL.options.UNLOCK_CHEST")
        {
            return [];
        }

        var player = warHistorianRepy.Owner!;
        var rewardRng = player.PlayerRng.Rewards.Clone();
        var potions = PredictionUtils.PredictPotionRewards(player, 2, rewardRng);
        var relics = OutOfCombatPredictionUtils.PredictRelicRewards(player, 2, rewardRng);
        return [
            .. potions.ToPredictionHoverTips(),
            .. OutOfCombatPredictionUtils.RelicTipsWithPickup(player, relics)
        ];
    }
}
