using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Events;
using RandomForeseer.Common.HoverTips;

namespace RandomForeseer.OutOfCombat.Events;

internal static class WuppyInTroublePrediction
{
    public static IReadOnlyList<IHoverTip> GetHoverTips(WuppyInTrouble wuppy, EventOption option)
    {
        return option.TextKey == "WUPPY_IN_TROUBLE.pages.INITIAL.options.IGNORE"
            ? [.. OutOfCombatPredictionUtils.PredictUniformPotions(wuppy.Owner!, 1).ToPredictionHoverTips()]
            : [];
    }
}
