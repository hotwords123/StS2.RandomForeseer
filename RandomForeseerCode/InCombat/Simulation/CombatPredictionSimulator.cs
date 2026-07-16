using System.Diagnostics.CodeAnalysis;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.Common.Mirrors;

namespace RandomForeseer.RandomForeseerCode.InCombat.Simulation;

internal sealed partial class CombatPredictionSimulator
{
    private readonly PredictionTrace _trace = new();

    public CombatPredictionState State { get; }

    public CombatPredictionRngSet Rng { get; }

    public PredictionStateStore StateStore { get; } = new();

    public CombatPredictionHistory History { get; }

    public CombatPredictionSimulator(ICombatState combatState)
    {
        State = new CombatPredictionState(combatState);
        Rng = CombatPredictionRngSet.From(combatState.RunState.Rng);
        History = new CombatPredictionHistory(_trace);
    }

    public PredictionRisk Snapshot()
    {
        return History.GetCurrentRisk();
    }

    public IDisposable PushActionSource(AbstractModel model, PredictionActionKind action)
    {
        return _trace.Push(model, PredictionInvocation.ForAction(action));
    }

    internal IDisposable PushMethodSource(AbstractModel model, MirrorMethodSpec method)
    {
        return _trace.Push(model, PredictionInvocation.ForMethod(method.BaseMethod));
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
