using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using RandomForeseer.Data;
using RandomForeseer.InCombat.Simulation;

namespace RandomForeseer.InCombat;

/// <summary>
/// Represents projected damage grouped by target without carrying display-surface-specific risk state.
/// </summary>
internal sealed record DamagePrediction(IReadOnlyList<DamagePredictionTarget> Targets)
{
    public static DamagePrediction Empty { get; } = new([]);

    public bool HasTargets => Targets.Count > 0;
}

/// <summary>
/// Contains the ordered damage lines projected for one creature.
/// </summary>
internal sealed record DamagePredictionTarget(
    Creature Target,
    IReadOnlyList<DamagePredictionLine> DamageLines)
{
    public decimal TotalDamage => DamageLines.Sum(static line => line.Damage);

    public decimal TotalUnblockedDamage => DamageLines.Sum(static line => line.UnblockedDamage);

    public bool WasTargetKilled => DamageLines.Any(static line => line.WasTargetKilled);
}

/// <summary>
/// Describes one recorded damage result and its immediate prediction trace source.
/// </summary>
internal sealed record DamagePredictionLine(
    decimal Damage,
    decimal UnblockedDamage,
    bool WasTargetKilled,
    AbstractModel Source);

/// <summary>
/// Converts accepted damage history entries into presentation-neutral grouped damage payloads.
/// </summary>
internal static class DamagePredictionProjector
{
    /// <summary>
    /// Groups damage entries by receiver while preserving each receiver's history order.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when an accepted damage entry has no source trace.</exception>
    public static DamagePrediction Project(IEnumerable<CombatPredictionDamageReceivedEntry> history)
    {
        var settings = ModData.Settings;
        if (!settings.CombatDamagePredictionEnabled)
        {
            return DamagePrediction.Empty;
        }

        var targets = history
            .GroupBy(static entry => entry.Receiver)
            .Select(group => new DamagePredictionTarget(
                group.Key,
                [.. group.Select(static entry => new DamagePredictionLine(
                    entry.Result.TotalDamage,
                    entry.Result.UnblockedDamage,
                    entry.Result.WasTargetKilled,
                    entry.Trace?.Source
                        ?? throw new InvalidOperationException("Damage entry has no source trace.")))]))
            .ToList();

        return targets.Count == 0 ? DamagePrediction.Empty : new DamagePrediction(targets);
    }

    public static bool ShouldIncludeEntry(CombatPredictionDamageReceivedEntry entry)
    {
        var settings = ModData.Settings;

        if (!settings.OrbDamagePredictionEnabled &&
            entry.Trace!.Ancestors().Any(static frame => frame.Source is OrbModel))
        {
            return false;
        }

        if (!settings.RandomTargetAttackPredictionEnabled &&
            entry.Trace!.Ancestors().Any(static frame =>
                frame.Source is CardModel { Type: CardType.Attack, TargetType: TargetType.RandomEnemy }))
        {
            return false;
        }

        return true;
    }
}
