using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Events;
using RandomForeseer.RandomForeseerCode.Common;

namespace RandomForeseer.RandomForeseerCode.OutOfCombat.Events;

internal static class PunchOffPrediction
{
    public static IReadOnlyList<IHoverTip> GetHoverTips(PunchOff punchOff, EventOption option)
    {
        var player = punchOff.Owner!;
        return option.TextKey switch
        {
            "PUNCH_OFF.pages.INITIAL.options.NAB" =>
                OutOfCombatPredictionUtils.RelicTipsWithPickup(
                    player,
                    OutOfCombatPredictionUtils.PredictRelicRewards(player, 1)),
            "PUNCH_OFF.pages.INITIAL.options.I_CAN_TAKE_THEM" =>
                PredictFightRewards(player),
            "PUNCH_OFF.pages.I_CAN_TAKE_THEM.options.FIGHT" => PredictFightRewards(player),
            _ => []
        };
    }

    private static IReadOnlyList<IHoverTip> PredictFightRewards(Player player)
    {
        if (!RandomForeseerSettings.IsFairPredictionAllowed(PredictionFairness.UnfairInAllModes))
        {
            return [];
        }

        var context = new RunPredictionContext(player);

        // Punch Off is a combat-layout event, so its monsters and unique HP Niche rolls
        // are already generated when the event room opens. Choosing Fight reuses that
        // internal combat state and does not consume another monster HP roll here.
        CombatEndEffectPrediction.FastForwardMonsterRoomCombatEndHooks(context);

        foreach (var runPlayer in context.RunState.Players)
        {
            // Punch Off creates rewards for every player in run order. Prior players use their
            // own Rewards RNG while sharing the same run-level Niche sequence.
            OutOfCombatPredictionUtils.FastForwardMonsterRoomRewards(context.ForPlayer(runPlayer));

            if (runPlayer == context.Player)
            {
                break;
            }
        }

        var relics = OutOfCombatPredictionUtils.PredictRelicRewards(context, 1);
        var potion = PotionFactory.CreateRandomPotionOutOfCombat(player, context.Rng.Rewards);

        var tips = OutOfCombatPredictionUtils.RelicTipsWithPickup(player, relics).ToList();
        tips.AddRange(PredictionHoverTips.Potions([potion]));
        return tips;
    }
}
