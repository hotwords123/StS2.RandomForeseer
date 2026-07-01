using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands.Builders;
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

    public void Execute(AttackCommand attackCommand)
    {
        // Mirrors AttackCommand.Execute only as a placeholder in Phase 1/2. Full attack
        // target and hit-count simulation is planned after the damage/death path is stable.
        // Vanilla also runs BeforeAttack/AfterAttack, modifies hit count, refreshes possible
        // targets per hit, consumes CombatTargets RNG for random attacks, and calls
        // CreatureCmd.Damage. Those are skipped until the attack mirror can return structured
        // hit results and update shadow state without advancing real attack callbacks.
        if (attackCommand.ModelSource != null)
        {
            using (PushSource(attackCommand.ModelSource))
            {
                MarkCurrentSourceRisky();
            }
        }
    }
}
