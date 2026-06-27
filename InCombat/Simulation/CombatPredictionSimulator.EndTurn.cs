using MegaCrit.Sts2.Core.Entities.Players;
using RandomForeseer.InCombat.Hooks;

namespace RandomForeseer.InCombat.Simulation;

internal sealed partial class CombatPredictionSimulator
{
    public void SimulateEndTurnEffects(IReadOnlyList<Player> playersEndingTurn)
    {
        foreach (var player in playersEndingTurn)
        {
            EndTurnHooks.RunAfterAutoPostPlayPhaseEntered(new AfterAutoPostPlayHookContext
            {
                Player = player,
                Simulator = this
            });
        }

        EndTurnHooks.RunBeforeSideTurnEnd(new BeforeSideTurnEndHookContext
        {
            Side = State.CombatState.CurrentSide,
            Simulator = this,
            Participants = playersEndingTurn.Select(static player => player.Creature).ToList()
        });

        foreach (var player in playersEndingTurn)
        {
            SimulateOrbQueueBeforeTurnEnd(player);
        }
    }
}
