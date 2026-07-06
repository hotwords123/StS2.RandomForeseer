using System.Diagnostics.CodeAnalysis;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using RandomForeseer.RandomForeseerCode.Common;

namespace RandomForeseer.RandomForeseerCode.InCombat.Simulation;

internal sealed partial class CombatPredictionSimulator(ICombatState combatState)
{
    private readonly PredictionSourceStack _sourceStack = new();
    private readonly PredictionRiskTracker _riskTracker = new();
    public CombatPredictionState State { get; } = new(combatState);

    public CombatPredictionRngSet Rng { get; } = CombatPredictionRngSet.From(combatState.RunState.Rng);

    public PredictionStateStore StateStore { get; } = new();

    public PredictionRisk Snapshot()
    {
        return _riskTracker.Snapshot();
    }

    public IDisposable PushSource(AbstractModel model)
    {
        return _sourceStack.Push(model);
    }

    public void MarkCurrentSourceRisky()
    {
        _riskTracker.AddCurrentSources(_sourceStack);
    }

    public static bool TryCreate(Player player, [NotNullWhen(true)] out CombatPredictionSimulator? simulator)
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
