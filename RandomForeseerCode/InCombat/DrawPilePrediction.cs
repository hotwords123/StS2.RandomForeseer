using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.HoverTips;
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
        return new(playerCombatState.DrawPile.Cards, simulator.Snapshot());
    }
}

internal sealed record DrawPilePredictionResult(IReadOnlyList<PredictedCard> Cards, PredictionRisk Risk)
{
    public static DrawPilePredictionResult Empty { get; } = new([], PredictionRisk.None);

    public static DrawPilePredictionResult FromDrawHistory(CombatPredictionSimulator simulator)
    {
        var history = simulator.History
            .OfType<CombatPredictionCardDrawnEntry>()
            .Select(simulator.History.GetResolvedEntry<CombatPredictionCardDrawResolvedEntry>)
            .ToList();
        return new([.. history.Select(entry => entry.Card)], simulator.History.GetRisk(history));
    }

    public static DrawPilePredictionResult FromAutoPlayHistory(CombatPredictionSimulator simulator)
    {
        var history = simulator.History
            .OfType<CombatPredictionAutoPlayFromDrawPileEntry>()
            .ToList();
        return new([.. history.Select(entry => entry.Card)], simulator.History.GetRisk(history));
    }

    public IReadOnlyList<IHoverTip> ToHoverTips()
    {
        var tips = PredictionHoverTips.Cards(Cards.Select(card => card.Preview)).ToList();
        PredictionHoverTips.AddDriftWarningIfNeeded(tips, "draw_pile", Risk);
        return tips;
    }
}
