using System.Diagnostics.CodeAnalysis;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using RandomForeseer.RandomForeseerCode.Common;

namespace RandomForeseer.RandomForeseerCode.InCombat.Simulation;

internal sealed partial class CombatPredictionSimulator
{
    private readonly PredictionSourceStack _sourceStack = new();
    private readonly PredictionRiskTracker _riskTracker = new();

    public CombatPredictionState State { get; }

    public CombatPredictionRngSet Rng { get; }

    public PredictionStateStore StateStore { get; } = new();

    public CombatPredictionHistory History { get; }

    public CombatPredictionSimulator(ICombatState combatState)
    {
        State = new CombatPredictionState(combatState);
        Rng = CombatPredictionRngSet.From(combatState.RunState.Rng);
        History = new CombatPredictionHistory(_riskTracker);
    }

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
