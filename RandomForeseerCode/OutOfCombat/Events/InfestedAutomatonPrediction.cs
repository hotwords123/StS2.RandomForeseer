using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Runs;
using RandomForeseer.RandomForeseerCode.Common;

namespace RandomForeseer.RandomForeseerCode.OutOfCombat.Events;

internal static class InfestedAutomatonPrediction
{
    public static IReadOnlyList<IHoverTip> GetHoverTips(InfestedAutomaton infestedAutomaton, EventOption option)
    {
        var player = infestedAutomaton.Owner!;
        return option.TextKey switch
        {
            "INFESTED_AUTOMATON.pages.INITIAL.options.STUDY" =>
                PredictionHoverTips.Cards(CardRewardPrediction.PredictCards(
                    player,
                    1,
                    CardCreationOptions.ForNonCombatWithDefaultOdds([player.Character.CardPool], card => card.Type == CardType.Power))),
            "INFESTED_AUTOMATON.pages.INITIAL.options.TOUCH_CORE" =>
                PredictionHoverTips.Cards(CardRewardPrediction.PredictCards(
                    player,
                    1,
                    CardCreationOptions.ForNonCombatWithDefaultOdds(
                            [player.Character.CardPool],
                            card => card.EnergyCost is { Canonical: 0, CostsX: false })
                        .WithFlags(CardCreationFlags.NoCardPoolModifications))),
            _ => []
        };
    }
}
