using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Events;

namespace RandomForeseer;

internal static class TheLegendsWereTruePrediction
{
    public static void Register()
    {
        EventOptionPredictionRegistry.Register<TheLegendsWereTrue>(GetHoverTips);
    }

    private static IReadOnlyList<IHoverTip> GetHoverTips(TheLegendsWereTrue legends, EventOption option)
    {
        return option.TextKey == "THE_LEGENDS_WERE_TRUE.pages.INITIAL.options.SLOWLY_FIND_AN_EXIT"
            ? PredictionHoverTips.Potions(OutOfCombatPredictionUtils.PredictUniformPotions(
                legends.Owner!,
                1,
                legends.Owner!.PlayerRng.Rewards))
            : [];
    }
}
