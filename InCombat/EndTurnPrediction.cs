using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Runs;
using RandomForeseer.InCombat.Simulation;

namespace RandomForeseer.InCombat;

internal static class EndTurnPrediction
{
    public static DamagePredictionResult PredictDamage()
    {
        if (!ShouldPredict() || CombatManager.Instance._state is not { } combatState)
        {
            return DamagePredictionResult.Empty;
        }

        var extraTurnPlayers = CombatManager.Instance.PlayersTakingExtraTurn;
        var playersEndingTurn = extraTurnPlayers.Count > 0
            ? extraTurnPlayers.ToList()
            : combatState.Players.ToList();

        var simulator = new CombatPredictionSimulator(combatState);
        simulator.SimulateEndTurnEffects(playersEndingTurn);

        return DamagePredictionResult.FromDamageHistory(simulator);
    }

    public static bool ShouldPredict()
    {
        if (!RandomForeseerSettings.IsPredictionFeatureEnabled(RandomForeseerSettings.EnableEndTurnPrediction) ||
            CombatManager.Instance._state?.CurrentSide != CombatSide.Player ||
            !CombatManager.Instance.IsInProgress ||
            RunManager.Instance.ActionQueueSynchronizer.CombatState != ActionSynchronizerCombatState.PlayPhase)
        {
            return false;
        }

        return true;
    }
}
