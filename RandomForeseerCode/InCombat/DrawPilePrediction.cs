using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;

namespace RandomForeseer.RandomForeseerCode.InCombat;

internal static class DrawPilePrediction
{
    public static DrawPilePredictionResult PredictShuffleAfterDrawPileDepleted(Player player)
    {
        if (!CombatPredictionSimulator.TryCreate(player, out var simulator))
        {
            return DrawPilePredictionResult.Empty;
        }

        var playerCombatState = simulator.State.GetPlayerCombatState(player);
        if (playerCombatState.DiscardPile.IsEmpty)
        {
            return DrawPilePredictionResult.Empty;
        }

        playerCombatState.DrawPile.Clear();
        simulator.Shuffle(player);
        return DrawPilePredictionResult.FromPredictedCards(playerCombatState.DrawPile.Cards, simulator.Snapshot());
    }
}

internal sealed record DrawPilePredictionResult(IReadOnlyList<CardModel> Cards, PredictionRisk Risk)
{
    public static DrawPilePredictionResult Empty { get; } = new([], PredictionRisk.None);

    public static DrawPilePredictionResult FromPredictedCards(IEnumerable<PredictedCard> cards, PredictionRisk risk)
    {
        return new DrawPilePredictionResult(cards.Select(card => card.Preview).ToArray(), risk);
    }

    public static DrawPilePredictionResult FromDrawHistory(CombatPredictionSimulator simulator)
    {
        var history = simulator.History
            .OfType<CombatPredictionCardDrawnEntry>()
            .ToList();
        var cards = history.Select(entry => entry.Card.Preview).ToArray();
        return new DrawPilePredictionResult(cards, simulator.History.GetRisk(history));
    }

    public IReadOnlyList<IHoverTip> ToHoverTips()
    {
        var tips = PredictionHoverTips.Cards(Cards).ToList();
        PredictionHoverTips.AddDriftWarningIfNeeded(tips, "draw_pile", Risk);
        return tips;
    }
}
