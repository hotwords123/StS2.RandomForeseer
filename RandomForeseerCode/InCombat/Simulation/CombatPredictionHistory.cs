using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using RandomForeseer.RandomForeseerCode.Common;

namespace RandomForeseer.RandomForeseerCode.InCombat.Simulation;

/// <summary>
/// Stores prediction-only combat events in simulation order without touching live combat history.
/// Parallel risk checkpoints let consumers evaluate risk at their last relevant entry without event producers
/// needing to know which prediction consumes it.
/// </summary>
internal sealed class CombatPredictionHistory(PredictionRiskTracker riskTracker)
{
    private readonly List<CombatPredictionHistoryEntry> _entries = [];
    private readonly List<PredictionRiskCheckpoint> _riskCheckpoints = [];

    public IReadOnlyList<CombatPredictionHistoryEntry> Entries => _entries;

    public IEnumerable<TEntry> OfType<TEntry>()
        where TEntry : CombatPredictionHistoryEntry
    {
        return _entries.OfType<TEntry>();
    }

    public PredictionRisk GetRiskAt(CombatPredictionHistoryEntry? entry)
    {
        if (entry is null)
        {
            return PredictionRisk.None;
        }

        var index = _entries.FindIndex(candidate => ReferenceEquals(candidate, entry));
        if (index < 0)
        {
            throw new InvalidOperationException("The history entry does not belong to this history.");
        }

        return riskTracker.Snapshot(_riskCheckpoints[index]);
    }

    public void CardAfflicted(PredictedCard card, AfflictionModel affliction)
    {
        Record(new CombatPredictionCardAfflictedEntry(card, affliction));
    }

    public void CardDrawn(PredictedCard card, bool fromHandDraw)
    {
        Record(new CombatPredictionCardDrawnEntry(card, fromHandDraw));
    }

    public void CreatureAttacked(
        Creature attacker,
        AbstractModel? source,
        IReadOnlyList<DamageResult> hitResults)
    {
        Record(new CombatPredictionCreatureAttackedEntry(attacker, source, hitResults));
    }

    public void DamageReceived(
        Creature receiver,
        Creature? dealer,
        DamageResult result,
        PredictedCard? cardSource,
        AbstractModel? sourceModel)
    {
        Record(new CombatPredictionDamageReceivedEntry(
            receiver,
            result,
            dealer,
            cardSource,
            sourceModel));
    }

    public void OrbChanneled(OrbModel orb)
    {
        Record(new CombatPredictionOrbChanneledEntry(orb));
    }

    private void Record(CombatPredictionHistoryEntry entry)
    {
        _entries.Add(entry);
        _riskCheckpoints.Add(riskTracker.Checkpoint);
    }
}

internal abstract record CombatPredictionHistoryEntry;

internal sealed record CombatPredictionDamageReceivedEntry(
    Creature Receiver,
    DamageResult Result,
    Creature? Dealer,
    PredictedCard? CardSource,
    AbstractModel? SourceModel) : CombatPredictionHistoryEntry;

internal sealed record CombatPredictionCreatureAttackedEntry(
    Creature Attacker,
    AbstractModel? SourceModel,
    IReadOnlyList<DamageResult> HitResults) : CombatPredictionHistoryEntry;

internal sealed record CombatPredictionOrbChanneledEntry(OrbModel Orb) : CombatPredictionHistoryEntry;

internal sealed record CombatPredictionCardDrawnEntry(
    PredictedCard Card,
    bool FromHandDraw) : CombatPredictionHistoryEntry;

internal sealed record CombatPredictionCardAfflictedEntry(
    PredictedCard Card,
    AfflictionModel Affliction) : CombatPredictionHistoryEntry;
