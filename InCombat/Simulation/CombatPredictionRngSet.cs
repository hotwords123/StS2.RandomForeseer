using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using RandomForeseer.Common;

namespace RandomForeseer.InCombat.Simulation;

internal sealed class CombatPredictionRngSet(RunRngSet rng)
{
    public Rng Shuffle { get; init; } = PredictionUtils.CloneRng(rng.Shuffle);

    public Rng CombatEnergyCosts { get; init; } = PredictionUtils.CloneRng(rng.CombatEnergyCosts);

    public Rng CombatTargets { get; init; } = PredictionUtils.CloneRng(rng.CombatTargets);

    public Rng CombatOrbGeneration { get; init; } = PredictionUtils.CloneRng(rng.CombatOrbGeneration);
}
