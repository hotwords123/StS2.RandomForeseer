using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Runs;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.Data;
using RandomForeseer.RandomForeseerCode.InCombat.Extensions;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;

namespace RandomForeseer.RandomForeseerCode.InCombat;

internal static class EndTurnPrediction
{
    public static EndTurnPredictionResult? Predict()
    {
        if (CombatManager.Instance.LiveCombatState is not { } combatState)
        {
            return null;
        }

        using var session = new CombatPredictionSession(combatState);
        var simulator = session.Simulator;
        simulator.SimulateEndPlayerTurn();
        return EndTurnPredictionResult.FromDamageHistory(simulator);
    }

    public static bool ShouldPredict()
    {
        var settings = ModData.Settings;
        return settings.IsPredictionEnabled && settings.EndTurnPredictionEnabled &&
            CombatManager.Instance.LiveCombatState?.CurrentSide is CombatSide.Player &&
            RunManager.Instance.ActionQueueSynchronizer.CombatState is ActionSynchronizerCombatState.PlayPhase;
    }
}

/// <summary>
/// Couples an end-turn damage payload with the risk accumulated through its last relevant damage entry.
/// </summary>
internal sealed record EndTurnPredictionResult(
    DamagePrediction DamagePrediction,
    PredictionRisk Risk)
{
    /// <summary>
    /// Projects all damage entries from a completed end-turn simulation and derives their shared risk boundary.
    /// </summary>
    public static EndTurnPredictionResult FromDamageHistory(CombatPredictionSimulator simulator)
    {
        var history = simulator.History
            .OfType<CombatPredictionDamageReceivedEntry>()
            .Where(DamagePredictionProjector.ShouldIncludeEntry)
            .ToList();
        return new EndTurnPredictionResult(
            DamagePredictionProjector.Project(history),
            simulator.History.GetRisk(history));
    }
}
