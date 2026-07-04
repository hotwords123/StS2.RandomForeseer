using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;

namespace RandomForeseer.RandomForeseerCode.InCombat;

internal static class CardDrawPrediction
{
    public static IReadOnlyList<IHoverTip> GetCardHoverTips(CardModel card)
    {
        if (!RandomForeseerSettings.IsPredictionFeatureEnabled(RandomForeseerSettings.EnableCardDrawPrediction))
        {
            return [];
        }

        return Predict(card).ToHoverTips();
    }

    private static DrawPilePredictionResult Predict(CardModel card)
    {
        return card switch
        {
            CalculatedGamble => PredictCalculatedGamble(card),
            Reboot reboot => PredictReboot(reboot),
            _ => DrawPilePredictionResult.Empty
        };
    }

    private static DrawPilePredictionResult PredictReboot(Reboot reboot)
    {
        if (reboot.Owner.Creature.CombatState is not { } combatState)
        {
            return DrawPilePredictionResult.Empty;
        }

        // Mirrors Reboot.OnPlay: after the source card leaves hand, move the remaining hand
        // into the draw pile, shuffle, then draw.
        var simulator = new CombatPredictionSimulator(combatState);
        simulator.RemoveFromHand(reboot);
        simulator.MoveHandToDrawPile(reboot.Owner);
        simulator.Shuffle(reboot.Owner);
        return simulator.Draw(reboot.Owner, reboot.DynamicVars.Cards.IntValue);
    }

    private static DrawPilePredictionResult PredictCalculatedGamble(CardModel source)
    {
        if (source.Owner.Creature.CombatState is not { } combatState)
        {
            return DrawPilePredictionResult.Empty;
        }

        // Mirrors CalculatedGamble.OnPlay and CardCmd.DiscardAndDraw up to the Draw step.
        // Sly cards are auto-played after Draw in the original method; this prediction does
        // not simulate them yet.
        var simulator = new CombatPredictionSimulator(combatState);
        simulator.RemoveFromHand(source);
        var cardsToDraw = simulator.DiscardHand(source.Owner);
        return simulator.Draw(source.Owner, cardsToDraw);
    }
}
