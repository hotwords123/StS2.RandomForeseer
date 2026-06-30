using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Rewards;

namespace RandomForeseer.RandomForeseerCode.OutOfCombat;

internal static class PaelsWingSacrificePrediction
{
    public static IReadOnlyList<IHoverTip> GetHoverTips(CardReward reward, CardRewardAlternative alternative)
    {
        if (!RandomForeseerSettings.IsPredictionFeatureEnabled(RandomForeseerSettings.EnablePaelsWingSacrificePrediction) ||
            alternative.OnSelect.Target is not PaelsWing paelsWing ||
            (paelsWing.RewardsSacrificed + 1) % paelsWing.DynamicVars["Sacrifices"].IntValue != 0)
        {
            return [];
        }

        return OutOfCombatPredictionUtils.RelicTipsWithPickup(
            reward.Player,
            OutOfCombatPredictionUtils.PredictRelicRewards(reward.Player, 1));
    }
}
