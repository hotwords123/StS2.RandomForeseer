using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.Common.Mirrors;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors;

internal abstract class CombatPredictionMirrorContext<TBase> : IPredictionMirrorContext<TBase>
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

    IDisposable IPredictionMirrorContext<TBase>.PushDispatchSource(TBase receiver, MirrorMethodSpec method)
    {
        return Simulator.PushMethodSource(GetDispatchSource(receiver), method);
    }

    void IPredictionMirrorContext<TBase>.RecordMethodNotMirroredRisk()
    {
        History.RecordRisk(PredictionRiskReason.MethodNotMirrored);
    }
}

internal abstract class CombatPredictionMirrorContext : CombatPredictionMirrorContext<AbstractModel>;
