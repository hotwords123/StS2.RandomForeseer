using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;

namespace RandomForeseer.RandomForeseerCode.InCombat;

internal sealed class CardDrawPrediction(CombatPredictionSimulator simulator, PredictedCard source)
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
        if (!CombatPredictionSimulator.TryCreate(card.Owner, out var simulator))
        {
            return DrawPilePredictionResult.Empty;
        }

        var predictedCard = simulator.State.FindCard(card) ?? new PredictedCard(card);

        using (simulator.PushSource(card))
        {
            simulator.AddToPile(predictedCard, PileType.Play);
            return new CardDrawPrediction(simulator, predictedCard).Predict();
        }
    }

    private DrawPilePredictionResult Predict()
    {
        return source.Preview switch
        {
            CalculatedGamble => PredictCalculatedGamble(),
            Reboot => PredictReboot(),
            _ => DrawPilePredictionResult.Empty
        };
    }

    private DrawPilePredictionResult PredictReboot()
    {
        // Mirrors Reboot.OnPlay: after the source card leaves hand, move the remaining hand
        // into the draw pile, shuffle, then draw.
        simulator.MoveHandToDrawPile(source.Preview.Owner);
        simulator.Shuffle(source.Preview.Owner);
        simulator.Draw(source.Preview.Owner, source.Preview.DynamicVars.Cards.IntValue);
        return DrawPilePredictionResult.FromDrawHistory(simulator);
    }

    private DrawPilePredictionResult PredictCalculatedGamble()
    {
        // Mirrors CalculatedGamble.OnPlay and CardCmd.DiscardAndDraw up to the Draw step.
        // Sly cards are auto-played after Draw in the original method; this prediction does
        // not simulate them yet.
        var playerCombatState = simulator.State.GetPlayerCombatState(source.Preview.Owner);
        var cards = playerCombatState.Hand.Cards.ToArray();
        simulator.DiscardAndDraw(cards, cards.Length, triggerSly: false);
        return DrawPilePredictionResult.FromDrawHistory(simulator);
    }
}
