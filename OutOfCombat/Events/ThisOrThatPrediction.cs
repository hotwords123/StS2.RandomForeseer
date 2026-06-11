using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Events;

namespace RandomForeseer.OutOfCombat.Events;

internal static class ThisOrThatPrediction
{
    public static IReadOnlyList<IHoverTip> GetHoverTips(ThisOrThat thisOrThat, EventOption option)
    {
        return option.TextKey == "THIS_OR_THAT.pages.INITIAL.options.ORNATE"
            ? OutOfCombatPredictionUtils.RelicTipsWithPickup(
                thisOrThat.Owner!,
                OutOfCombatPredictionUtils.PredictRelicRewards(thisOrThat.Owner!, 1))
            : [];
    }
}
