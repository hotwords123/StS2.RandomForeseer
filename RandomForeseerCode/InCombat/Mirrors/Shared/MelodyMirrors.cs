using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using RandomForeseer.InCombat.Simulation;

namespace RandomForeseer.InCombat.Mirrors.Shared;

internal static class MelodyMirrors
{
    /// <summary>Mirrors <see cref="MelodyCmd.PlayedThisTurn"/>.</summary>
    public static bool PlayedThisTurn(CombatPredictionSimulator simulator, Player player, CardTag tag)
    {
        return CombatManager.Instance.History.CardPlaysFinished.Any(entry =>
                   entry.HappenedThisTurn(simulator.State.CombatState) &&
                   entry.CardPlay.Player == player && entry.CardPlay.Card.Tags.Contains(tag)) ||
               simulator.History.OfType<CombatPredictionCardPlayFinishedEntry>().Any(entry =>
                   entry.CardPlay.Player == player && entry.Card.Preview.Tags.Contains(tag));
    }

    /// <summary>Mirrors <see cref="MelodyCmd.MelodiesThisTurn"/>.</summary>
    public static int MelodiesThisTurn(CombatPredictionSimulator simulator, Player player)
    {
        return (PlayedThisTurn(simulator, player, CardTag.Wah) ? 1 : 0) +
               (PlayedThisTurn(simulator, player, CardTag.Cho) ? 1 : 0) +
               (PlayedThisTurn(simulator, player, CardTag.Ki) ? 1 : 0);
    }

    public static int CountStartedThisTurn(CombatPredictionSimulator simulator, Player player, CardTag tag)
    {
        return CombatManager.Instance.History.CardPlaysStarted.Count(entry =>
                   entry.HappenedThisTurn(simulator.State.CombatState) &&
                   entry.CardPlay.Player == player && entry.CardPlay.Card.Tags.Contains(tag)) +
               simulator.History.OfType<CombatPredictionCardPlayStartedEntry>().Count(entry =>
                   entry.CardPlay.Player == player && entry.Card.Preview.Tags.Contains(tag));
    }
}
