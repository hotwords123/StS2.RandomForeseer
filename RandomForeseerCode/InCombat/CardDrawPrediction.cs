using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;

namespace RandomForeseer.RandomForeseerCode.InCombat;

internal static class CardDrawPrediction
{
    public static IReadOnlyList<IHoverTip> GetCardHoverTips(CardModel card)
    {
        if (!RandomForeseerSettings.IsPredictionFeatureEnabled(RandomForeseerSettings.EnableCardDrawPrediction) ||
            card is not (CalculatedGamble or Reboot))
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
        simulator.ManualPlay(predictedCard, target: null);
        return DrawPilePredictionResult.FromDrawHistory(simulator);
    }
}
