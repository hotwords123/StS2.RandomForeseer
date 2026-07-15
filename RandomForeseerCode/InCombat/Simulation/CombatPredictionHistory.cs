using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using RandomForeseer.RandomForeseerCode.Common;

namespace RandomForeseer.RandomForeseerCode.InCombat.Simulation;

/// <summary>
/// Stores prediction-only combat events in simulation order without touching live combat history.
/// Parallel risk checkpoints let consumers evaluate risk through their relevant entries without entry producers
/// needing to know which prediction consumes them. A returned handle can move an entry's checkpoint to the point
/// where deferred processing finishes; otherwise the entry is considered complete when recorded.
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

    public PredictionRisk GetRisk(IReadOnlyList<CombatPredictionHistoryEntry> entries)
    {
        if (entries.Count == 0)
        {
            return PredictionRisk.None;
        }

        var latestCheckpoint = entries.Max(entry =>
        {
            var index = entry.Index;
            if (index < 0 || index >= _entries.Count || !ReferenceEquals(_entries[index], entry))
            {
                throw new InvalidOperationException("The history entry does not belong to this history.");
            }

            return _riskCheckpoints[index];
        });

        return riskTracker.Snapshot(latestCheckpoint);
    }

    public EntryHandle CardAfflicted(PredictedCard card, AfflictionModel affliction)
    {
        return Record(new CombatPredictionCardAfflictedEntry(_entries.Count, card, affliction));
    }

    public EntryHandle CardDrawn(PredictedCard card, bool fromHandDraw)
    {
        return Record(new CombatPredictionCardDrawnEntry(_entries.Count, card, fromHandDraw));
    }

    public EntryHandle CardsSelected(IReadOnlyList<PredictedCard> cards, AbstractModel? sourceModel)
    {
        return Record(new CombatPredictionCardsSelectedEntry(
            _entries.Count,
            SnapshotCards(cards),
            sourceModel));
    }

    public EntryHandle CardSelectionOptions(IReadOnlyList<PredictedCard> cards, AbstractModel? sourceModel)
    {
        return Record(new CombatPredictionCardSelectionOptionsEntry(
            _entries.Count,
            SnapshotCards(cards),
            sourceModel));
    }

    public EntryHandle CreatureAttacked(
        Creature attacker,
        AbstractModel? source,
        IReadOnlyList<DamageResult> hitResults)
    {
        return Record(new CombatPredictionCreatureAttackedEntry(_entries.Count, attacker, source, hitResults));
    }

    public EntryHandle DamageReceived(
        Creature receiver,
        Creature? dealer,
        DamageResult result,
        PredictedCard? cardSource,
        AbstractModel? sourceModel)
    {
        return Record(new CombatPredictionDamageReceivedEntry(
            _entries.Count,
            receiver,
            result,
            dealer,
            cardSource,
            sourceModel));
    }

    public EntryHandle OrbChanneled(OrbModel orb, AbstractModel? sourceModel)
    {
        return Record(new CombatPredictionOrbChanneledEntry(_entries.Count, orb, sourceModel));
    }

    private EntryHandle Record(CombatPredictionHistoryEntry entry)
    {
        if (entry.Index != _entries.Count)
        {
            throw new InvalidOperationException("History entry index does not match its list position.");
        }

        _entries.Add(entry);
        _riskCheckpoints.Add(riskTracker.Checkpoint);
        return new EntryHandle(this, entry.Index);
    }

    private static IReadOnlyList<PredictedCard> SnapshotCards(IEnumerable<PredictedCard> cards)
    {
        return [.. cards.Select(static card => card.Clone())];
    }

    private void Complete(int index)
    {
        _riskCheckpoints[index] = riskTracker.Checkpoint;
    }

    internal readonly struct EntryHandle(CombatPredictionHistory history, int index)
    {
        public void Complete()
        {
            history.Complete(index);
        }
    }
}

internal abstract record CombatPredictionHistoryEntry(int Index);

internal sealed record CombatPredictionDamageReceivedEntry(
    int Index,
    Creature Receiver,
    DamageResult Result,
    Creature? Dealer,
    PredictedCard? CardSource,
    AbstractModel? SourceModel) : CombatPredictionHistoryEntry(Index);

internal sealed record CombatPredictionCreatureAttackedEntry(
    int Index,
    Creature Attacker,
    AbstractModel? SourceModel,
    IReadOnlyList<DamageResult> HitResults) : CombatPredictionHistoryEntry(Index);

internal sealed record CombatPredictionOrbChanneledEntry(
    int Index,
    OrbModel Orb,
    AbstractModel? SourceModel) : CombatPredictionHistoryEntry(Index);

internal sealed record CombatPredictionCardDrawnEntry(
    int Index,
    PredictedCard Card,
    bool FromHandDraw) : CombatPredictionHistoryEntry(Index);

internal abstract record CombatPredictionCardSelectionEntry(
    int Index,
    IReadOnlyList<PredictedCard> Cards,
    AbstractModel? SourceModel) : CombatPredictionHistoryEntry(Index);

internal sealed record CombatPredictionCardsSelectedEntry(
    int Index,
    IReadOnlyList<PredictedCard> Cards,
    AbstractModel? SourceModel) : CombatPredictionCardSelectionEntry(Index, Cards, SourceModel);

internal sealed record CombatPredictionCardSelectionOptionsEntry(
    int Index,
    IReadOnlyList<PredictedCard> Cards,
    AbstractModel? SourceModel) : CombatPredictionCardSelectionEntry(Index, Cards, SourceModel);

internal sealed record CombatPredictionCardAfflictedEntry(
    int Index,
    PredictedCard Card,
    AfflictionModel Affliction) : CombatPredictionHistoryEntry(Index);
