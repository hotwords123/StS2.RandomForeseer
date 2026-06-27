using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Runs;
using RandomForeseer.Common;
using RandomForeseer.InCombat.Simulation;

namespace RandomForeseer.InCombat;

internal static class EndTurnPrediction
{
    public static CombatPredictionOverlayContent Predict()
    {
        if (!ShouldPredict() || CombatManager.Instance._state is not { } combatState)
        {
            return CombatPredictionOverlayContent.Empty;
        }

        var extraTurnPlayers = CombatManager.Instance.PlayersTakingExtraTurn;
        var playersEndingTurn = extraTurnPlayers.Count > 0
            ? extraTurnPlayers.ToList()
            : combatState.Players.ToList();

        var simulator = new CombatPredictionSimulator(combatState);
        simulator.SimulateEndTurnEffects(playersEndingTurn);

        var indicatorHoverTips = PredictionHoverTips.Text("end_turn_prediction_indicator").ToList();
        var risk = simulator.Snapshot();
        if (risk.HasRisk && RandomForeseerSettings.EnableDriftWarnings)
        {
            indicatorHoverTips.Add(PredictionHoverTips.DriftWarning("end_turn", risk));
        }

        return CombatPredictionOverlayContentFactory.FromDamageHistory(simulator, indicatorHoverTips);
    }

    public static bool ShouldPredict()
    {
        if (!RandomForeseerSettings.IsPredictionFeatureEnabled(RandomForeseerSettings.EnableEndTurnPrediction) ||
            CombatManager.Instance._state is not { CurrentSide: CombatSide.Player } combatState ||
            !CombatManager.Instance.IsInProgress ||
            RunManager.Instance.ActionQueueSynchronizer.CombatState != ActionSynchronizerCombatState.PlayPhase)
        {
            return false;
        }

        return true;
    }
}
