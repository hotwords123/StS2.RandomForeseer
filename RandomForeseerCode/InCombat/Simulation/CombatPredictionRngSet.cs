using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using RandomForeseer.RandomForeseerCode.Common;

namespace RandomForeseer.RandomForeseerCode.InCombat.Simulation;

internal sealed class CombatPredictionRngSet
{
    public required Rng Shuffle { get; init; }
    public required Rng CombatCardSelection { get; init; }
    public required Rng CombatEnergyCosts { get; init; }
    public required Rng CombatTargets { get; init; }
    public required Rng CombatOrbGeneration { get; init; }

    public static CombatPredictionRngSet From(RunRngSet rng)
    {
        return new CombatPredictionRngSet
        {
            Shuffle = PredictionUtils.CloneRng(rng.Shuffle),
            CombatCardSelection = PredictionUtils.CloneRng(rng.CombatCardSelection),
            CombatEnergyCosts = PredictionUtils.CloneRng(rng.CombatEnergyCosts),
            CombatTargets = PredictionUtils.CloneRng(rng.CombatTargets),
            CombatOrbGeneration = PredictionUtils.CloneRng(rng.CombatOrbGeneration)
        };
    }
}
