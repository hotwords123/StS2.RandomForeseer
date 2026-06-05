using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using RandomForeseer.Common;

namespace RandomForeseer.OutOfCombat.Events;

internal static class MorphicGrovePrediction
{
    public static void Register()
    {
        EventOptionPredictionRegistry.Register<MorphicGrove>(GetHoverTips);
    }

    private static IReadOnlyList<IHoverTip> GetHoverTips(MorphicGrove morphicGrove, EventOption option)
    {
        return option.TextKey == "MORPHIC_GROVE.pages.INITIAL.options.GROUP"
            ? PredictionHoverTips.CardBundles(PredictGroup(morphicGrove), isTransform: true)
            : [];
    }

    private static IReadOnlyList<IReadOnlyList<CardModel>> PredictGroup(MorphicGrove morphicGrove)
    {
        return OutOfCombatPredictionUtils.PredictDistinctDeckTransformResultBundles(
            morphicGrove.Owner!,
            morphicGrove.Rng,
            transformCount: 2);
    }
}
