using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Potions;

namespace RandomForeseer.InCombat;

internal static class PotionDrawPrediction
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
        var owner = potion.Owner;

        return potion switch
        {
            BottledPotential => PredictBottledPotential(owner, potion.DynamicVars.Cards.IntValue),
            Clarity => DrawPilePrediction.PredictDraw(owner, potion.DynamicVars.Cards.IntValue),
            CureAll => DrawPilePrediction.PredictDraw(owner, potion.DynamicVars.Cards.IntValue),
            GlowwaterPotion => PredictGlowwaterPotion(owner, potion.DynamicVars.Cards.IntValue),
            SneckoOil => PredictSneckoOil(owner, potion.DynamicVars.Cards.IntValue),
            SwiftPotion => DrawPilePrediction.PredictDraw(owner, potion.DynamicVars.Cards.IntValue),
            _ => DrawPilePredictionResult.Empty
        };
    }

    private static DrawPilePredictionResult PredictGlowwaterPotion(Player player, int count)
    {
        if (!DrawPilePrediction.TryCreate(player, out var prediction))
        {
            return DrawPilePredictionResult.Empty;
        }

        // Mirrors GlowwaterPotion.OnUse: exhaust current hand, then draw.
        prediction.ExhaustHand();
        return prediction.Draw(count);
    }

    private static DrawPilePredictionResult PredictSneckoOil(Player player, int count)
    {
        if (!DrawPilePrediction.TryCreate(player, out var prediction))
        {
            return DrawPilePredictionResult.Empty;
        }

        // Mirrors SneckoOil.OnUse: draw first, then randomize the full hand's non-X costs.
        prediction.Draw(count);
        return prediction.RandomizeHandCosts();
    }

    private static DrawPilePredictionResult PredictBottledPotential(Player player, int count)
    {
        if (!DrawPilePrediction.TryCreate(player, out var prediction))
        {
            return DrawPilePredictionResult.Empty;
        }

        // Mirrors BottledPotential.OnUse: CardPileCmd.Add(hand, Draw), CardPileCmd.Shuffle, CardPileCmd.Draw.
        prediction.MoveHandToDrawPile();
        prediction.Shuffle();
        return prediction.Draw(count);
    }
}
