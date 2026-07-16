using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using RandomForeseer.RandomForeseerCode.Common;

namespace RandomForeseer.RandomForeseerCode.InCombat.Simulation;

internal sealed class CombatPredictionRngSet
{
    public required Rng Shuffle { get; init; }
    public required Rng CombatCardGeneration { get; init; }
    public required Rng CombatPotionGeneration { get; init; }
    public required Rng CombatCardSelection { get; init; }
    public required Rng CombatEnergyCosts { get; init; }
    public required Rng CombatTargets { get; init; }
    public required Rng CombatOrbGeneration { get; init; }

    public static CombatPredictionRngSet From(RunRngSet rng)
    {
        return new CombatPredictionRngSet
        {
            Shuffle = rng.Shuffle.Clone(),
            CombatCardGeneration = rng.CombatCardGeneration.Clone(),
            CombatPotionGeneration = rng.CombatPotionGeneration.Clone(),
            CombatCardSelection = rng.CombatCardSelection.Clone(),
            CombatEnergyCosts = rng.CombatEnergyCosts.Clone(),
            CombatTargets = rng.CombatTargets.Clone(),
            CombatOrbGeneration = rng.CombatOrbGeneration.Clone()
        };
    }
}
