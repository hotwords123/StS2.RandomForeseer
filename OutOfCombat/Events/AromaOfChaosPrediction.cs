using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using RandomForeseer.Common;

namespace RandomForeseer.OutOfCombat.Events;

internal static class AromaOfChaosPrediction
{
    public static void Register()
    {
        EventOptionPredictionRegistry.Register<AromaOfChaos>(GetHoverTips);
    }

    private static IReadOnlyList<IHoverTip> GetHoverTips(AromaOfChaos aromaOfChaos, EventOption option)
    {
        return option.TextKey == "AROMA_OF_CHAOS.pages.INITIAL.options.LET_GO"
            ? PredictionHoverTips.Cards(PredictLetGo(aromaOfChaos))
            : [];
    }

    private static IReadOnlyList<CardModel> PredictLetGo(AromaOfChaos aromaOfChaos)
    {
        return OutOfCombatPredictionUtils.PredictDistinctDeckTransformResults(aromaOfChaos.Owner!, aromaOfChaos.Rng);
    }
}
