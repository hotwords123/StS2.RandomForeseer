using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Events;

namespace RandomForeseer.RandomForeseerCode.OutOfCombat.Events;

internal static class RanwidTheElderPrediction
{
    public static IReadOnlyList<IHoverTip> GetHoverTips(RanwidTheElder ranwid, EventOption option)
    {
        var player = ranwid.Owner!;
        return option.TextKey switch
        {
            "RANWID_THE_ELDER.pages.INITIAL.options.POTION" or
            "RANWID_THE_ELDER.pages.INITIAL.options.GOLD" =>
                OutOfCombatPredictionUtils.RelicTipsWithPickup(player, OutOfCombatPredictionUtils.PredictRelicRewards(player, 1)),
            "RANWID_THE_ELDER.pages.INITIAL.options.RELIC" =>
                OutOfCombatPredictionUtils.RelicTipsWithPickup(player, OutOfCombatPredictionUtils.PredictRelicRewards(player, 2)),
            _ => []
        };
    }
}
