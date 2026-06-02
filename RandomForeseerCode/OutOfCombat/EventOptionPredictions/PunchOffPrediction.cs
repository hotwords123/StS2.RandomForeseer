using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Events;

namespace RandomForeseer;

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
        return OutOfCombatPredictionUtils.RelicTipsWithPickup(
            player,
            OutOfCombatPredictionUtils.PredictRelicRewards(player, 1));
    }
}
