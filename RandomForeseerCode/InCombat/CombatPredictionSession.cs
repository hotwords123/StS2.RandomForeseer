using MegaCrit.Sts2.Core.Combat;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;

namespace RandomForeseer.RandomForeseerCode.InCombat;

/// <summary>Owns the legacy simulator for one synchronous prediction and its result projection.</summary>
/// <remarks>
/// Unlike <see cref="CombatPredictionInteractionSession"/>, this session is scoped to one computation in a using block,
/// even when the interaction continues displaying its result or requests another prediction. Entry-specific guards
/// and exception handling remain with the caller. This boundary does not yet provide an eager snapshot or freeze output models.
/// </remarks>
internal sealed class CombatPredictionSession : IDisposable
{
    private CombatPredictionSimulator? _simulator;

    public CombatPredictionSession(ICombatState combatState)
    {
        ArgumentNullException.ThrowIfNull(combatState);
        _simulator = new CombatPredictionSimulator(combatState);
    }

    /// <summary>Gets this session's simulator while the session is alive.</summary>
    /// <remarks>
    /// Callers must finish simulation and materialize the result inside the using block, without retaining the simulator
    /// or returning deferred work. Existing model references in presentation results remain supported until the
    /// output-freezing migration. Disposal prevents further getter access; it does not revoke previously acquired references.
    /// </remarks>
    public CombatPredictionSimulator Simulator
    {
        get
        {
            ObjectDisposedException.ThrowIf(_simulator is null, this);
            return _simulator;
        }
    }

    /// <summary>Releases session ownership without clearing or recycling models held by legacy results.</summary>
    public void Dispose()
    {
        // Legacy history and projections may still hold preview models. Clearing their data here would change
        // already-produced output; graph resource disposal/pooling requires the later output-freezing contract.
        _simulator = null;
    }
}
