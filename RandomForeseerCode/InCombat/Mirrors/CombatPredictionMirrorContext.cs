using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.Common.Mirrors;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors;

internal abstract class CombatPredictionMirrorContext : IPredictionMirrorContext<AbstractModel>
{
    public required CombatPredictionSimulator Simulator { get; init; }

    public CombatPredictionState State => Simulator.State;

    public CombatPredictionRngSet Rng => Simulator.Rng;

    public PredictionStateStore StateStore => Simulator.StateStore;

    public ICombatState CombatState => Simulator.State.CombatState;

    public IRunState RunState => CombatState.RunState;

    public IDisposable PushSource(AbstractModel model)
    {
        return Simulator.PushSource(model);
    }

    public void MarkCurrentSourceRisky()
    {
        Simulator.MarkCurrentSourceRisky();
    }
}
