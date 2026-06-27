using System.Diagnostics.CodeAnalysis;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using RandomForeseer.Common;
using RandomForeseer.InCombat.Simulation;

namespace RandomForeseer.InCombat;

internal static class DrawPilePrediction
{
    public static DrawPilePredictionResult PredictTopCardsAfterNecessaryShuffles(Player player, int count)
    {
        return TryCreateSimulator(player, out var simulator)
            ? simulator.PeekTopCardsAfterNecessaryShuffles(player, count)
            : DrawPilePredictionResult.Empty;
    }

    public static DrawPilePredictionResult PredictDraw(Player player, int count)
    {
        return TryCreateSimulator(player, out var simulator)
            ? simulator.Draw(player, count)
            : DrawPilePredictionResult.Empty;
    }

    public static DrawPilePredictionResult PredictShuffleAfterDrawPileDepleted(Player player)
    {
        return TryCreateSimulator(player, out var simulator)
            ? simulator.ShuffleAfterDrawPileDepleted(player)
            : DrawPilePredictionResult.Empty;
    }

    private static bool TryCreateSimulator(
        Player player,
        [NotNullWhen(true)] out CombatPredictionSimulator? simulator)
    {
        if (player.Creature.CombatState is not { } combatState)
        {
            simulator = null;
            return false;
        }

        simulator = new CombatPredictionSimulator(combatState);
        return true;
    }
}

internal sealed record DrawPilePredictionResult(IReadOnlyList<CardModel> Cards, PredictionRisk Risk)
{
    public bool HasDriftRisk => Risk.HasRisk;

    public static DrawPilePredictionResult Empty { get; } = new([], PredictionRisk.None);

    public static DrawPilePredictionResult FromPredictedCards(IEnumerable<PredictedCard> cards, PredictionRisk risk)
    {
        return new DrawPilePredictionResult(cards.Select(card => card.Preview).ToList(), risk);
    }

    public IReadOnlyList<IHoverTip> ToHoverTips()
    {
        var tips = PredictionHoverTips.Cards(Cards).ToList();
        if (HasDriftRisk && RandomForeseerSettings.EnableDriftWarnings)
        {
            tips.Add(PredictionHoverTips.DriftWarning("draw_pile", Risk));
        }

        return tips;
    }
}
