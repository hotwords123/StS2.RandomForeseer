using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Random;
using RandomForeseer.Common;

namespace RandomForeseer.OutOfCombat.Events;

internal static class PunchOffPrediction
{
    public static void Register()
    {
        EventOptionPredictionRegistry.Register<PunchOff>(GetHoverTips);
    }

    private static IReadOnlyList<IHoverTip> GetHoverTips(PunchOff punchOff, EventOption option)
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

        var rewardRng = PredictionUtils.CloneRng(player.PlayerRng.Rewards);
        var nicheRng = PredictionUtils.CloneRng(player.RunState.Rng.Niche);
        CombatEndEffectPrediction.FastForwardMonsterRoomCombatEndHooks(player, rewardRng, nicheRng);
        FastForwardPunchOffMonsterRewardsBeforePlayer(player, nicheRng);
        FastForwardPunchOffMonsterRewards(player, rewardRng, nicheRng);

        var relic = OutOfCombatPredictionUtils.PredictRelicRewards(player, 1, rewardRng)[0];
        var potion = PotionFactory.CreateRandomPotionOutOfCombat(player, rewardRng).ToMutable();

        var tips = OutOfCombatPredictionUtils.RelicTipsWithPickup(player, [relic]).ToList();
        tips.AddRange(PredictionHoverTips.Potions([potion]));
        return tips;
    }

    private static void FastForwardPunchOffMonsterRewardsBeforePlayer(Player player, Rng nicheRng)
    {
        foreach (var runPlayer in player.RunState.Players)
        {
            if (runPlayer == player)
            {
                break;
            }

            var rewardRng = PredictionUtils.CloneRng(runPlayer.PlayerRng.Rewards);
            FastForwardPunchOffMonsterRewards(runPlayer, rewardRng, nicheRng);
        }
    }

    private static void FastForwardPunchOffMonsterRewards(Player player, Rng rewardRng, Rng nicheRng)
    {
        var encounter = ModelDb.Encounter<PunchOffEventEncounter>();
        OutOfCombatPredictionUtils.FastForwardMonsterRoomRewards(
            player,
            rewardRng,
            nicheRng,
            encounter.MinGoldReward,
            encounter.MaxGoldReward);
    }
}
