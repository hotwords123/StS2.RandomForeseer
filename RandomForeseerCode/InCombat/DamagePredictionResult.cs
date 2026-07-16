using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;

namespace RandomForeseer.RandomForeseerCode.InCombat;

internal sealed record DamagePredictionResult(
    IReadOnlyList<DamagePredictionTarget> Targets,
    PredictionRisk Risk)
{
    public static DamagePredictionResult Empty { get; } = new([], PredictionRisk.None);

    public bool HasTargets => Targets.Count > 0;

    public bool HasRisk => Risk.HasRisk;

    public static DamagePredictionResult FromDamageHistory(CombatPredictionSimulator simulator)
    {
        var history = simulator.History
            .OfType<CombatPredictionDamageReceivedEntry>()
            .ToList();
        var targets = history
            .GroupBy(static entry => entry.Receiver)
            .Select(group => new DamagePredictionTarget(
                group.Key,
                group
                    .Select(static entry => new DamagePredictionLine(
                        entry.Result.TotalDamage,
                        entry.Result.UnblockedDamage,
                        entry.Result.WasTargetKilled,
                        entry.Trace!.Source))
                    .ToList()))
            .ToList();

        return new DamagePredictionResult(targets, simulator.History.GetRisk(history));
    }
}

internal sealed record DamagePredictionTarget(
    Creature Target,
    IReadOnlyList<DamagePredictionLine> DamageLines)
{
    public decimal TotalDamage => DamageLines.Sum(static line => line.Damage);

    public decimal TotalUnblockedDamage => DamageLines.Sum(static line => line.UnblockedDamage);

    public bool WasTargetKilled => DamageLines.Any(static line => line.WasTargetKilled);
}

internal sealed record DamagePredictionLine(
    decimal Damage,
    decimal UnblockedDamage,
    bool WasTargetKilled,
    AbstractModel Source);
