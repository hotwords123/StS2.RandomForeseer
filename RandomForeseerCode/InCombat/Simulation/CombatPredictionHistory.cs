using System.Runtime.InteropServices;
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
    private readonly Dictionary<Type, int> _entryCounts = [];

    public IReadOnlyList<CombatPredictionHistoryEntry> Entries => _entries;

    public IEnumerable<TEntry> OfType<TEntry>()
        where TEntry : CombatPredictionHistoryEntry
    {
        return _entries.OfType<TEntry>();
    }

    public int Count<TEntry>()
        where TEntry : CombatPredictionHistoryEntry
    {
        return _entryCounts.TryGetValue(typeof(TEntry), out var count) ? count : 0;
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
        return Record(new CombatPredictionCardAfflictedEntry
        {
            Index = _entries.Count,
            Card = card.Clone(),
            Affliction = affliction
        });
    }

    public EntryHandle CardDrawn(PredictedCard card, bool fromHandDraw)
    {
        return Record(new CombatPredictionCardDrawnEntry
        {
            Index = _entries.Count,
            Card = card.Clone(),
            FromHandDraw = fromHandDraw
        });
    }

    public EntryHandle CardsSelected(IReadOnlyList<PredictedCard> cards, AbstractModel? sourceModel)
    {
        return Record(new CombatPredictionCardsSelectedEntry
        {
            Index = _entries.Count,
            Cards = SnapshotCards(cards),
            SourceModel = sourceModel
        });
    }

    public EntryHandle CardSelectionOptions(IReadOnlyList<PredictedCard> cards, AbstractModel? sourceModel)
    {
        return Record(new CombatPredictionCardSelectionOptionsEntry
        {
            Index = _entries.Count,
            Cards = SnapshotCards(cards),
            SourceModel = sourceModel
        });
    }

    public EntryHandle CardsGenerated(IReadOnlyList<PredictedCard> cards, AbstractModel? sourceModel)
    {
        return Record(new CombatPredictionCardsGeneratedEntry
        {
            Index = _entries.Count,
            Cards = SnapshotCards(cards),
            SourceModel = sourceModel
        });
    }

    public EntryHandle CardGenerationOptions(IReadOnlyList<PredictedCard> cards, AbstractModel? sourceModel)
    {
        return Record(new CombatPredictionCardGenerationOptionsEntry
        {
            Index = _entries.Count,
            Cards = SnapshotCards(cards),
            SourceModel = sourceModel
        });
    }

    public EntryHandle AutoPlayFromDrawPile(PredictedCard card, AbstractModel? sourceModel)
    {
        return Record(new CombatPredictionAutoPlayFromDrawPileEntry
        {
            Index = _entries.Count,
            Card = card.Clone(),
            SourceModel = sourceModel
        });
    }

    public EntryHandle PotionGenerated(PotionModel potion, AbstractModel? sourceModel)
    {
        return Record(new CombatPredictionPotionGeneratedEntry
        {
            Index = _entries.Count,
            Potion = potion,
            SourceModel = sourceModel
        });
    }

    public EntryHandle CreatureAttacked(
        Creature attacker,
        AbstractModel? source,
        IReadOnlyList<DamageResult> hitResults)
    {
        return Record(new CombatPredictionCreatureAttackedEntry
        {
            Index = _entries.Count,
            Attacker = attacker,
            SourceModel = source,
            HitResults = hitResults
        });
    }

    public EntryHandle DamageReceived(
        Creature receiver,
        Creature? dealer,
        DamageResult result,
        PredictedCard? cardSource,
        AbstractModel? sourceModel)
    {
        return Record(new CombatPredictionDamageReceivedEntry
        {
            Index = _entries.Count,
            Receiver = receiver,
            Result = result,
            Dealer = dealer,
            CardSource = cardSource,
            SourceModel = sourceModel
        });
    }

    public EntryHandle OrbChanneled(OrbModel orb, AbstractModel? sourceModel)
    {
        return Record(new CombatPredictionOrbChanneledEntry
        {
            Index = _entries.Count,
            Orb = orb,
            SourceModel = sourceModel
        });
    }

    private EntryHandle Record(CombatPredictionHistoryEntry entry)
    {
        if (entry.Index != _entries.Count)
        {
            throw new InvalidOperationException("History entry index does not match its list position.");
        }

        _entries.Add(entry);
        _riskCheckpoints.Add(riskTracker.Checkpoint);
        CollectionsMarshal.GetValueRefOrAddDefault(_entryCounts, entry.GetType(), out _)++;
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

internal abstract class CombatPredictionHistoryEntry
{
    public required int Index { get; init; }
}

internal sealed class CombatPredictionDamageReceivedEntry : CombatPredictionHistoryEntry
{
    public required Creature Receiver { get; init; }
    public required DamageResult Result { get; init; }
    public required Creature? Dealer { get; init; }
    public required PredictedCard? CardSource { get; init; }
    public required AbstractModel? SourceModel { get; init; }
}

internal sealed class CombatPredictionCreatureAttackedEntry : CombatPredictionHistoryEntry
{
    public required Creature Attacker { get; init; }
    public required AbstractModel? SourceModel { get; init; }
    public required IReadOnlyList<DamageResult> HitResults { get; init; }
}

internal sealed class CombatPredictionOrbChanneledEntry : CombatPredictionHistoryEntry
{
    public required OrbModel Orb { get; init; }
    public required AbstractModel? SourceModel { get; init; }
}

internal sealed class CombatPredictionCardDrawnEntry : CombatPredictionHistoryEntry
{
    public required PredictedCard Card { get; init; }
    public required bool FromHandDraw { get; init; }
}

internal abstract class CombatPredictionCardSelectionEntry : CombatPredictionHistoryEntry
{
    public required IReadOnlyList<PredictedCard> Cards { get; init; }
    public required AbstractModel? SourceModel { get; init; }
}

internal sealed class CombatPredictionCardsSelectedEntry : CombatPredictionCardSelectionEntry;

internal sealed class CombatPredictionCardSelectionOptionsEntry : CombatPredictionCardSelectionEntry;

internal abstract class CombatPredictionCardGenerationEntry : CombatPredictionHistoryEntry
{
    public required IReadOnlyList<PredictedCard> Cards { get; init; }
    public required AbstractModel? SourceModel { get; init; }
}

internal sealed class CombatPredictionCardsGeneratedEntry : CombatPredictionCardGenerationEntry;

internal sealed class CombatPredictionCardGenerationOptionsEntry : CombatPredictionCardGenerationEntry;

internal sealed class CombatPredictionCardAfflictedEntry : CombatPredictionHistoryEntry
{
    public required PredictedCard Card { get; init; }
    public required AfflictionModel Affliction { get; init; }
}

internal sealed class CombatPredictionAutoPlayFromDrawPileEntry : CombatPredictionHistoryEntry
{
    public required PredictedCard Card { get; init; }
    public required AbstractModel? SourceModel { get; init; }
}

internal sealed class CombatPredictionPotionGeneratedEntry : CombatPredictionHistoryEntry
{
    public required PotionModel Potion { get; init; }
    public required AbstractModel? SourceModel { get; init; }
}
