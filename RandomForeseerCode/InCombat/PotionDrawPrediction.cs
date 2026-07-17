using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Potions;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;

namespace RandomForeseer.RandomForeseerCode.InCombat;

internal sealed class PotionDrawPrediction(CombatPredictionSimulator simulator, Player target, PotionModel source)
{
    public static IReadOnlyList<IHoverTip> GetPotionHoverTips(PotionPredictionContext context)
    {
        if (!RandomForeseerSettings.IsPredictionFeatureEnabled(RandomForeseerSettings.EnablePotionDrawPrediction) ||
            !context.Source.IsMutable ||
            context.SourceOwner.Creature.CombatState == null ||
            context.Target.Creature.CombatState == null)
        {
            return [];
        }

        return Predict(context).ToHoverTips();
    }

    private static DrawPilePredictionResult Predict(PotionPredictionContext context)
    {
        if (!CombatPredictionSimulator.TryCreate(context.Target, out var simulator))
        {
            return DrawPilePredictionResult.Empty;
        }

        using (simulator.PushActionSource(context.Source, PredictionActionKind.PotionUse))
        {
            return new PotionDrawPrediction(simulator, context.Target, context.Source).Predict();
        }
    }

    private DrawPilePredictionResult Predict()
    {
        return source switch
        {
            BottledPotential => PredictBottledPotential(),
            Clarity => PredictDraw(),
            CureAll => PredictDraw(),
            GlowwaterPotion => PredictGlowwaterPotion(),
            SneckoOil => PredictSneckoOil(),
            SwiftPotion => PredictDraw(),
            _ => DrawPilePredictionResult.Empty
        };
    }

    private DrawPilePredictionResult PredictDraw()
    {
        simulator.Draw(target, source.DynamicVars.Cards.IntValue);
        return DrawPilePredictionResult.FromDrawHistory(simulator);
    }

    private DrawPilePredictionResult PredictGlowwaterPotion()
    {
        // Mirrors GlowwaterPotion.OnUse: exhaust current hand, then draw.
        simulator.ExhaustHand(target);
        simulator.Draw(target, source.DynamicVars.Cards.IntValue);
        return DrawPilePredictionResult.FromDrawHistory(simulator);
    }

    private DrawPilePredictionResult PredictSneckoOil()
    {
        // Mirrors SneckoOil.OnUse: draw first, then randomize the full hand's non-X costs.
        var hand = simulator.State.GetPlayerCombatState(target).Hand;

        simulator.Draw(target, source.DynamicVars.Cards.IntValue);

        foreach (var card in hand.Cards)
        {
            if (card.Preview.EnergyCost.CostsX ||
                card.Preview.EnergyCost.GetWithModifiers(CostModifiers.None) < 0)
            {
                continue;
            }

            card.MutablePreview.EnergyCost.SetThisTurnOrUntilPlayed(simulator.Rng.CombatEnergyCosts.NextInt(4));
        }

        return new(hand.Cards, simulator.Snapshot());
    }

    private DrawPilePredictionResult PredictBottledPotential()
    {
        // Mirrors BottledPotential.OnUse: CardPileCmd.Add(hand, Draw), CardPileCmd.Shuffle, CardPileCmd.Draw.
        simulator.MoveHandToDrawPile(target);
        simulator.Shuffle(target);
        simulator.Draw(target, source.DynamicVars.Cards.IntValue);
        return DrawPilePredictionResult.FromDrawHistory(simulator);
    }
}
