using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using RandomForeseer.RandomForeseerCode.InCombat.Mirrors;

namespace RandomForeseer.RandomForeseerCode.InCombat.Simulation;

/// <summary>
/// Mirrors <see cref="AttackContext"/>.
/// </summary>
internal sealed class SimAttackContext : IDisposable
{
    private readonly CombatPredictionSimulator _simulator;
    private readonly AttackCommand _attackCommand;
    private bool _disposed;

    /// <summary>
    /// Private constructor. Use <see cref="CombatPredictionSimulator.CreateAttackContext"/> to create instances.
    /// </summary>
    private SimAttackContext(CombatPredictionSimulator simulator, CardPlay cardPlay)
    {
        _simulator = simulator;
        _attackCommand = new AttackCommand(0m)
            .FromCard(cardPlay.Card, cardPlay)
            .TargetingAllOpponents(simulator.State.CombatState);
    }

    /// <summary>
    /// Mirrors <see cref="AttackContext.CreateAsync"/>.
    /// </summary>
    public static SimAttackContext Create(CombatPredictionSimulator simulator, CardPlay cardPlay)
    {
        var context = new SimAttackContext(simulator, cardPlay);
        HookMirrors.BeforeAttack(context._simulator, context._attackCommand);
        return context;
    }

    /// <summary>
    /// Mirrors <see cref="AttackContext.AddHit"/>.
    /// </summary>
    public void AddHit(IEnumerable<DamageResult> results)
    {
        _attackCommand.IncrementHitsInternal();
        _attackCommand.AddResultsInternal(results);
    }

    /// <summary>
    /// Mirrors <see cref="AttackContext.DisposeAsync"/>.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        HookMirrors.AfterAttack(_simulator, _attackCommand);
    }
}
