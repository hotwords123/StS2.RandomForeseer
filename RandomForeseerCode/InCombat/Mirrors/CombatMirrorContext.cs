using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using RandomForeseer.Common;
using RandomForeseer.Common.Mirrors;
using RandomForeseer.InCombat.Simulation;

namespace RandomForeseer.InCombat.Mirrors;

internal abstract class CombatMirrorContext<TBase> : IMethodMirrorContext<TBase>
    where TBase : AbstractModel
{
    public required CombatPredictionSimulator Simulator { get; init; }

    public CombatPredictionState State => Simulator.State;

    public CombatPredictionRngSet Rng => Simulator.Rng;

    public PredictionStateStore StateStore => Simulator.StateStore;

    public CombatPredictionHistory History => Simulator.History;

    public ICombatState CombatState => Simulator.State.CombatState;

    public IRunState RunState => CombatState.RunState;

    protected virtual AbstractModel GetDispatchSource(TBase receiver) => receiver;

    IDisposable IMethodMirrorContext<TBase>.PushDispatchSource(TBase receiver, MirrorMethodSpec method)
    {
        return Simulator.PushMethodSource(GetDispatchSource(receiver), method);
    }

    void IMethodMirrorContext<TBase>.RecordMethodNotMirroredRisk()
    {
        History.RecordRisk(PredictionRiskReason.MethodNotMirrored);
    }

    void IMethodMirrorContext<TBase>.RecordMethodMirrorIncompleteRisk()
    {
        History.RecordRisk(PredictionRiskReason.MethodMirrorIncomplete);
    }
}

internal abstract class CombatMirrorContext : CombatMirrorContext<AbstractModel>;
