using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Potions;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;

namespace RandomForeseer.RandomForeseerCode.InCombat;

internal static class AutoPlayFromDrawPilePrediction
{
    public static IReadOnlyList<IHoverTip> GetCardHoverTips(CardModel card)
    {
        if (!RandomForeseerSettings.IsPredictionFeatureEnabled(RandomForeseerSettings.EnableAutoPlayFromDrawPilePrediction) ||
            card is not (Havoc or Cascade) ||
            !CombatPredictionSimulator.TryCreate(card.Owner, out var simulator))
        {
            return [];
        }

        var predictedCard = simulator.State.FindCard(card) ?? new PredictedCard(card);
        simulator.ManualPlay(predictedCard, target: null);
        return DrawPilePredictionResult.FromAutoPlayHistory(simulator).ToHoverTips();
    }

    public static IReadOnlyList<IHoverTip> GetPotionHoverTips(PotionPredictionContext context)
    {
        if (!RandomForeseerSettings.IsPredictionFeatureEnabled(RandomForeseerSettings.EnableAutoPlayFromDrawPilePrediction) ||
            !context.Source.IsMutable ||
            context.SourceOwner.Creature.CombatState == null ||
            context.Target.Creature.CombatState == null)
        {
            return [];
        }

        var count = context.Source switch
        {
            DistilledChaos => context.Source.DynamicVars.Repeat.IntValue,
            _ => 0
        };

        return Predict(context, count).ToHoverTips();
    }

    private static DrawPilePredictionResult Predict(PotionPredictionContext context, int count)
    {
        if (count <= 0 || !CombatPredictionSimulator.TryCreate(context.Target, out var simulator))
        {
            return DrawPilePredictionResult.Empty;
        }

        using (simulator.PushActionSource(context.Source, PredictionActionKind.PotionUse))
        {
            simulator.AutoPlayFromDrawPile(context.Target, count, CardPilePosition.Top);
        }

        return DrawPilePredictionResult.FromAutoPlayHistory(simulator);
    }
}
