using MegaCrit.Sts2.Core.Entities.Players;
using RandomForeseer.RandomForeseerCode.InCombat.Mirrors;

namespace RandomForeseer.RandomForeseerCode.InCombat.Simulation;

internal sealed partial class CombatPredictionSimulator
{
    public void SimulateEndTurnEffects(IReadOnlyList<Player> playersEndingTurn)
    {
        foreach (var player in playersEndingTurn)
        {
            HookMirrors.AfterAutoPostPlayPhaseEntered(this, player);
        }

        HookMirrors.BeforeSideTurnEnd(
            this,
            State.CombatState.CurrentSide,
            [.. playersEndingTurn.Select(static player => player.Creature)]);

        foreach (var player in playersEndingTurn)
        {
            SimulateOrbQueueBeforeTurnEnd(player);
        }
    }
}
