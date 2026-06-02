using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;

namespace RandomForeseer;

internal static class MorphicGrovePrediction
{
    public static void Register()
    {
        EventOptionPredictionRegistry.Register<MorphicGrove>(GetHoverTips);
    }

    private static IReadOnlyList<IHoverTip> GetHoverTips(MorphicGrove morphicGrove, EventOption option)
    {
        return option.TextKey == "MORPHIC_GROVE.pages.INITIAL.options.GROUP"
            ? PredictionHoverTips.CardBundles(PredictGroup(morphicGrove))
            : [];
    }

    private static IReadOnlyList<IReadOnlyList<CardModel>> PredictGroup(MorphicGrove morphicGrove)
    {
        return OutOfCombatPredictionUtils.PredictDistinctDeckTransformResultBundles(
            morphicGrove.Owner!,
            morphicGrove.Owner!.RunState.Rng.Niche,
            transformCount: 2);
    }
}
