using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Runs;
using RandomForeseer.Common;

namespace RandomForeseer.OutOfCombat.Events;

internal static class BrainLeechPrediction
{
    public static void Register()
    {
        EventOptionPredictionRegistry.Register<BrainLeech>(GetHoverTips);
    }

    private static IReadOnlyList<IHoverTip> GetHoverTips(BrainLeech brainLeech, EventOption option)
    {
        var player = brainLeech.Owner!;
        return option.TextKey switch
        {
            "BRAIN_LEECH.pages.INITIAL.options.SHARE_KNOWLEDGE" =>
                PredictionHoverTips.Cards(OutOfCombatPredictionUtils.PredictCards(
                    player,
                    brainLeech.DynamicVars["FromCardChoiceCount"].IntValue,
                    CardCreationOptions.ForNonCombatWithDefaultOdds([player.Character.CardPool]))),
            "BRAIN_LEECH.pages.INITIAL.options.RIP" =>
                PredictionHoverTips.CardBundles(OutOfCombatPredictionUtils.PredictCardRewardBundles(
                    player,
                    brainLeech.DynamicVars["RewardCount"].IntValue,
                    3,
                    CardCreationOptions.ForNonCombatWithDefaultOdds([ModelDb.CardPool<ColorlessCardPool>()])
                        .WithFlags(CardCreationFlags.NoRarityModification | CardCreationFlags.NoCardPoolModifications))),
            _ => []
        };
    }
}
