using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Runs;
using RandomForeseer.Data;
using RandomForeseer.InCombat.Extensions;

namespace RandomForeseer.InCombat;

internal static class EndTurnPrediction
{
    public static CombatPredictionProjection? Predict()
    {
        if (CombatManager.Instance.LiveCombatState is not { } combatState ||
            LocalContext.GetMe(combatState) is not { } player)
        {
            return null;
        }

        using var session = new CombatPredictionSession(combatState);
        var simulator = session.Simulator;
        simulator.SimulateEndPlayerTurn();
        return CombatPredictionProjector.Project(simulator.History, rootFrame: null, playerFilter: player);
    }

    public static bool ShouldPredict()
    {
        var settings = ModData.Settings;
        return settings.IsPredictionEnabled && settings.EndTurnPredictionEnabled &&
            CombatManager.Instance.LiveCombatState?.CurrentSide is CombatSide.Player &&
            RunManager.Instance.ActionQueueSynchronizer.CombatState is ActionSynchronizerCombatState.PlayPhase;
    }
}
