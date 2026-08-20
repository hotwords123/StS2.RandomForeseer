using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Hooks;
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

    public PredictionTraceFrame? CurrentFrame => _trace.Current;

    /// <summary>
    /// Mirrors <see cref="CombatTurnState.IsInProgress"/>.
    /// </summary>
    public bool IsInProgress { get; private set; } = true;

    /// <summary>
    /// Mirrors <see cref="CombatTurnState.PendingLoss"/>.
    /// </summary>
    public bool IsAboutToLose { get; private set; }

    /// <summary>
    /// Mirrors <see cref="CombatManager.IsEnding"/>.
    /// </summary>
    public bool IsEnding => IsCombatEnding();

    /// <summary>
    /// Mirrors <see cref="CombatManager.IsOverOrEnding"/>.
    /// </summary>
    public bool IsOverOrEnding => !IsInProgress || IsEnding;

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

    /// <summary>
    /// Mirrors the prediction-relevant boundary of <see cref="CombatManager.LoseCombat"/>.
    /// </summary>
    public void LoseCombat()
    {
        IsAboutToLose = true;
    }

    /// <summary>
    /// Mirrors the prediction-relevant boundary of <see cref="CombatManager.CheckWinCondition"/>.
    /// </summary>
    /// <remarks>
    /// This only evaluates the shadow pending-loss/victory state and commits the simulator's
    /// <see cref="IsInProgress"/> flag when the combat has reached a safe point.
    /// It does not simulate the vanilla combat teardown after <c>EndCombatInternal</c>, including
    /// after-combat hooks, rewards, room progression, save operations, music/UI cleanup, or run-loss handling.
    /// </remarks>
    public bool CheckWinCondition()
    {
        if (!IsAboutToLose && !IsEnding)
        {
            return false;
        }

        IsAboutToLose = false;
        IsInProgress = false;
        return true;
    }

    public IDisposable PushActionSource(AbstractModel model, PredictionActionKind action)
    {
        return _trace.Push(model, PredictionInvocation.ForAction(action));
    }

    public IDisposable PushMethodSource(AbstractModel model, MirrorMethodSpec method)
    {
        return _trace.Push(model, PredictionInvocation.ForMethod(method.BaseMethod));
    }

    private bool IsCombatEnding()
    {
        if (!IsInProgress)
        {
            return false;
        }

        if (IsAboutToLose)
        {
            return true;
        }

        return !State.Enemies.Any(enemy => State.GetCreature(enemy).IsAlive && enemy.IsPrimaryEnemy) &&
               !Hook.ShouldStopCombatFromEnding(State.CombatState);
    }
}
