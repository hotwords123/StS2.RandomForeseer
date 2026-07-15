using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Potions;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;

namespace RandomForeseer.RandomForeseerCode.InCombat;

internal sealed class PotionDrawPrediction(CombatPredictionSimulator simulator, Player player, PotionModel source)
{
    public static IReadOnlyList<IHoverTip> GetPotionHoverTips(PotionModel potion)
    {
        if (!RandomForeseerSettings.IsPredictionFeatureEnabled(RandomForeseerSettings.EnablePotionDrawPrediction) ||
            !potion.IsMutable ||
            potion.Owner.Creature.CombatState == null)
        {
            return [];
        }

        return Predict(potion).ToHoverTips();
    }

    private static DrawPilePredictionResult Predict(PotionModel potion)
    {
        var player = potion.Owner;

        if (!CombatPredictionSimulator.TryCreate(player, out var simulator))
        {
            return DrawPilePredictionResult.Empty;
        }

        using (simulator.PushSource(potion))
        {
            return new PotionDrawPrediction(simulator, player, potion).Predict();
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
        simulator.Draw(player, source.DynamicVars.Cards.IntValue);
        return DrawPilePredictionResult.FromDrawHistory(simulator);
    }

    private DrawPilePredictionResult PredictGlowwaterPotion()
    {
        // Mirrors GlowwaterPotion.OnUse: exhaust current hand, then draw.
        simulator.ExhaustHand(source.Owner);
        simulator.Draw(source.Owner, source.DynamicVars.Cards.IntValue);
        return DrawPilePredictionResult.FromDrawHistory(simulator);
    }

    private DrawPilePredictionResult PredictSneckoOil()
    {
        // Mirrors SneckoOil.OnUse: draw first, then randomize the full hand's non-X costs.
        var hand = simulator.State.GetPlayerCombatState(player).Hand;

        simulator.Draw(player, source.DynamicVars.Cards.IntValue);

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
        simulator.MoveHandToDrawPile(player);
        simulator.Shuffle(player);
        simulator.Draw(player, source.DynamicVars.Cards.IntValue);
        return DrawPilePredictionResult.FromDrawHistory(simulator);
    }
}
