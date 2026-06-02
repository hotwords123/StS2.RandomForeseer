using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;

namespace RandomForeseer;

internal static class WhisperingHollowPrediction
{
    public static void Register()
    {
        EventOptionPredictionRegistry.Register<WhisperingHollow>(GetHoverTips);
    }

    private static IReadOnlyList<IHoverTip> GetHoverTips(WhisperingHollow whisperingHollow, EventOption option)
    {
        return option.TextKey switch
        {
            "WHISPERING_HOLLOW.pages.INITIAL.options.GOLD" =>
                PredictionHoverTips.Potions(OutOfCombatPredictionUtils.PredictPotionRewards(whisperingHollow.Owner!, 2, whisperingHollow.Owner!.PlayerRng.Rewards)),
            "WHISPERING_HOLLOW.pages.INITIAL.options.HUG" =>
                PredictionHoverTips.Cards(PredictHug(whisperingHollow)),
            _ => []
        };
    }

    private static IReadOnlyList<CardModel> PredictHug(WhisperingHollow whisperingHollow)
    {
        return OutOfCombatPredictionUtils.PredictDistinctDeckTransformResults(whisperingHollow.Owner!, whisperingHollow.Rng);
    }
}
