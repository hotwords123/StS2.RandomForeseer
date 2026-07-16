using System.Runtime.InteropServices;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using RandomForeseer.RandomForeseerCode.Common;

namespace RandomForeseer.RandomForeseerCode.InCombat.Simulation;

/// <summary>
/// Stores prediction-only combat events in simulation order without touching live combat history.
/// Deferred events use separate original and resolved entries; the resolved entry carries the final snapshot and
/// risk checkpoint while the original entry determines semantic order.
/// </summary>
internal sealed class CombatPredictionHistory(PredictionRiskTracker riskTracker)
{
    private readonly List<CombatPredictionHistoryEntry> _entries = [];
    private readonly List<PredictionRiskCheckpoint> _riskCheckpoints = [];
    private readonly Dictionary<Type, int> _entryCounts = [];
    private readonly Dictionary<CombatPredictionHistoryEntry, CombatPredictionHistoryEntry> _completions =
        new(ReferenceEqualityComparer.Instance);

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

        foreach (var entry in entries)
        {
            ValidateOwnership(entry);
        }

        var latestCheckpoint = entries.Max(entry => _riskCheckpoints[entry.Index]);
        return riskTracker.Snapshot(latestCheckpoint);
    }

    public void CardAfflicted(PredictedCard card, AfflictionModel affliction)
    {
        Record(new CombatPredictionCardAfflictedEntry
        {
            Index = _entries.Count,
            Card = card.Clone(),
            Affliction = affliction
        });
    }

    public CombatPredictionCardDrawnEntry CardDrawn(PredictedCard card, bool fromHandDraw)
    {
        return Record(new CombatPredictionCardDrawnEntry
        {
            Index = _entries.Count,
            Card = card.Clone(),
            FromHandDraw = fromHandDraw
        });
    }

    public void CardDrawResolved(CombatPredictionCardDrawnEntry originalEntry, PredictedCard card)
    {
        Complete(originalEntry, new CombatPredictionCardDrawResolvedEntry
        {
            Index = _entries.Count,
            OriginalEntry = originalEntry,
            Card = card.Clone()
        });
    }

    public void CardsSelected(IReadOnlyList<PredictedCard> cards, AbstractModel? sourceModel)
    {
        Record(new CombatPredictionCardsSelectedEntry
        {
            Index = _entries.Count,
            Cards = SnapshotCards(cards),
            SourceModel = sourceModel
        });
    }

    public CombatPredictionCardGeneratedEntry CardGenerated(PredictedCard card, AbstractModel? sourceModel)
    {
        return Record(new CombatPredictionCardGeneratedEntry
        {
            Index = _entries.Count,
            Card = card.Clone(),
            SourceModel = sourceModel
        });
    }

    public void CardGenerationResolved(CombatPredictionCardGeneratedEntry originalEntry, PredictedCard card)
    {
        Complete(originalEntry, new CombatPredictionCardGenerationResolvedEntry
        {
            Index = _entries.Count,
            OriginalEntry = originalEntry,
            Card = card.Clone()
        });
    }

    public void CardGenerationOptions(IReadOnlyList<PredictedCard> cards, AbstractModel? sourceModel)
    {
        Record(new CombatPredictionCardGenerationOptionsEntry
        {
            Index = _entries.Count,
            Cards = SnapshotCards(cards),
            SourceModel = sourceModel
        });
    }

    public void AutoPlayFromDrawPile(PredictedCard card, AbstractModel? sourceModel)
    {
        Record(new CombatPredictionAutoPlayFromDrawPileEntry
        {
            Index = _entries.Count,
            Card = card.Clone(),
            SourceModel = sourceModel
        });
    }

    public void PotionGenerated(PotionModel potion, AbstractModel? sourceModel)
    {
        Record(new CombatPredictionPotionGeneratedEntry
        {
            Index = _entries.Count,
            Potion = potion,
            SourceModel = sourceModel
        });
    }

    public void CreatureAttacked(
        Creature attacker,
        AbstractModel? source,
        IReadOnlyList<DamageResult> hitResults)
    {
        Record(new CombatPredictionCreatureAttackedEntry
        {
            Index = _entries.Count,
            Attacker = attacker,
            SourceModel = source,
            HitResults = hitResults
        });
    }

    public void DamageReceived(
        Creature receiver,
        Creature? dealer,
        DamageResult result,
        PredictedCard? cardSource,
        AbstractModel? sourceModel)
    {
        Record(new CombatPredictionDamageReceivedEntry
        {
            Index = _entries.Count,
            Receiver = receiver,
            Result = result,
            Dealer = dealer,
            CardSource = cardSource,
            SourceModel = sourceModel
        });
    }

    public void OrbChanneled(OrbModel orb, AbstractModel? sourceModel)
    {
        Record(new CombatPredictionOrbChanneledEntry
        {
            Index = _entries.Count,
            Orb = orb,
            SourceModel = sourceModel
        });
    }

    public TResolved GetResolvedEntry<TResolved>(CombatPredictionHistoryEntry originalEntry)
        where TResolved : CombatPredictionHistoryEntry
    {
        var resolvedEntry = _completions.GetValueOrDefault(originalEntry)
            ?? throw new InvalidOperationException("The deferred history entry has not been resolved.");

        return resolvedEntry as TResolved
            ?? throw new InvalidOperationException("The deferred history entry has an invalid resolution type.");
    }

    private TEntry Record<TEntry>(TEntry entry)
        where TEntry : CombatPredictionHistoryEntry
    {
        if (entry.Index != _entries.Count)
        {
            throw new InvalidOperationException("History entry index does not match its list position.");
        }

        _entries.Add(entry);
        _riskCheckpoints.Add(riskTracker.Checkpoint);
        CollectionsMarshal.GetValueRefOrAddDefault(_entryCounts, entry.GetType(), out _)++;
        return entry;
    }

    private static IReadOnlyList<PredictedCard> SnapshotCards(IEnumerable<PredictedCard> cards)
    {
        return [.. cards.Select(static card => card.Clone())];
    }

    private void Complete(CombatPredictionHistoryEntry originalEntry, CombatPredictionHistoryEntry resolvedEntry)
    {
        ValidateOwnership(originalEntry);
        if (_completions.ContainsKey(originalEntry))
        {
            throw new InvalidOperationException("The deferred history entry has already been resolved.");
        }

        Record(resolvedEntry);
        _completions.Add(originalEntry, resolvedEntry);
    }

    private void ValidateOwnership(CombatPredictionHistoryEntry entry)
    {
        var index = entry.Index;
        if (index < 0 || index >= _entries.Count || !ReferenceEquals(_entries[index], entry))
        {
            throw new InvalidOperationException("The history entry does not belong to this history.");
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

internal sealed class CombatPredictionCardDrawResolvedEntry : CombatPredictionHistoryEntry
{
    public required CombatPredictionCardDrawnEntry OriginalEntry { get; init; }
    public required PredictedCard Card { get; init; }
}

internal sealed class CombatPredictionCardsSelectedEntry : CombatPredictionHistoryEntry
{
    public required IReadOnlyList<PredictedCard> Cards { get; init; }
    public required AbstractModel? SourceModel { get; init; }
}

internal abstract class CombatPredictionCardGenerationEntry : CombatPredictionHistoryEntry
{
    public required AbstractModel? SourceModel { get; init; }
}

internal sealed class CombatPredictionCardGeneratedEntry : CombatPredictionCardGenerationEntry
{
    public required PredictedCard Card { get; init; }
}

internal sealed class CombatPredictionCardGenerationResolvedEntry : CombatPredictionHistoryEntry
{
    public required CombatPredictionCardGeneratedEntry OriginalEntry { get; init; }
    public required PredictedCard Card { get; init; }
}

internal sealed class CombatPredictionCardGenerationOptionsEntry : CombatPredictionCardGenerationEntry
{
    public required IReadOnlyList<PredictedCard> Cards { get; init; }
}

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
