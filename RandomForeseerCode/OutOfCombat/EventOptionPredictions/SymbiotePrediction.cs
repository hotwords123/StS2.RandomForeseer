using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;

namespace RandomForeseer;

internal static class SymbiotePrediction
{
    public static void Register()
    {
        EventOptionPredictionRegistry.Register<Symbiote>(GetHoverTips);
    }

    private static IReadOnlyList<IHoverTip> GetHoverTips(Symbiote symbiote, EventOption option)
    {
        return option.TextKey == "SYMBIOTE.pages.INITIAL.options.KILL_WITH_FIRE"
            ? PredictionHoverTips.Cards(PredictKillWithFire(symbiote))
            : [];
    }

    private static IReadOnlyList<CardModel> PredictKillWithFire(Symbiote symbiote)
    {
        return OutOfCombatPredictionUtils.PredictDistinctDeckTransformResults(symbiote.Owner!, symbiote.Rng);
    }
}
