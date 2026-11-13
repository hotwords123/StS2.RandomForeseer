using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Events;
using RandomForeseer.Common;

namespace RandomForeseer.OutOfCombat.Events;

internal static class RanwidTheElderPrediction
{
    public static IReadOnlyList<IHoverTip> GetHoverTips(RanwidTheElder ranwid, EventOption option)
    {
        var player = ranwid.Owner!;
        return option.TextKey switch
        {
            "RANWID_THE_ELDER.pages.INITIAL.options.POTION" or
            "RANWID_THE_ELDER.pages.INITIAL.options.GOLD" =>
                OutOfCombatPredictionUtils.RelicTipsWithPickup(
                    player,
                    OutOfCombatPredictionUtils.PredictRelicRewards(player, 1, ranwid.Rng.Clone())),
            "RANWID_THE_ELDER.pages.INITIAL.options.RELIC" =>
                OutOfCombatPredictionUtils.RelicTipsWithPickup(
                    player,
                    OutOfCombatPredictionUtils.PredictRelicRewards(player, 2, ranwid.Rng.Clone())),
            _ => []
        };
    }
}
