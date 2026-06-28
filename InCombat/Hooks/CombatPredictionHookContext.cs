using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using RandomForeseer.Common;
using RandomForeseer.Common.Hooks;
using RandomForeseer.InCombat.Simulation;

namespace RandomForeseer.InCombat.Hooks;

internal abstract class CombatPredictionHookContext : IPredictionHookContext
{
    public required CombatPredictionSimulator Simulator { get; init; }

    public CombatPredictionState State => Simulator.State;

    public CombatPredictionRngSet Rng => Simulator.Rng;

    public PredictionStateStore StateStore => Simulator.StateStore;

    public ICombatState CombatState => Simulator.State.CombatState;

    public IRunState RunState => CombatState.RunState;

    public virtual bool ShouldContinue => true;

    public IDisposable PushSource(AbstractModel model)
    {
        return Simulator.PushSource(model);
    }

    public void MarkCurrentSourceRisky()
    {
        Simulator.MarkCurrentSourceRisky();
    }
}
