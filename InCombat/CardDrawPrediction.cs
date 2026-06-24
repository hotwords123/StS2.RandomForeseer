using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;

namespace RandomForeseer.InCombat;

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
        if (!DrawPilePrediction.TryCreate(reboot.Owner, out var prediction))
        {
            return DrawPilePredictionResult.Empty;
        }

        // Mirrors Reboot.OnPlay: after the source card leaves hand, move the remaining hand
        // into the draw pile, shuffle, then draw.
        prediction.RemoveFromHand(reboot);
        prediction.MoveHandToDrawPile();
        prediction.Shuffle();
        return prediction.Draw(reboot.DynamicVars.Cards.IntValue);
    }

    private static DrawPilePredictionResult PredictCalculatedGamble(CardModel source)
    {
        if (!DrawPilePrediction.TryCreate(source.Owner, out var prediction))
        {
            return DrawPilePredictionResult.Empty;
        }

        // Mirrors CalculatedGamble.OnPlay and CardCmd.DiscardAndDraw up to the Draw step.
        // Sly cards are auto-played after Draw in the original method; this prediction does
        // not simulate them yet.
        prediction.RemoveFromHand(source);
        var cardsToDraw = prediction.DiscardHand();
        return prediction.Draw(cardsToDraw);
    }
}
