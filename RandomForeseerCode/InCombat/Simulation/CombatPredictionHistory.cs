using System.Runtime.InteropServices;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using RandomForeseer.RandomForeseerCode.Common;

namespace RandomForeseer.RandomForeseerCode.InCombat.Simulation;

/// <summary>
/// Stores prediction-only combat events in simulation order without touching live combat history.
/// Deferred events use separate original and resolved entries; the resolved entry carries the final snapshot and
/// risk boundary while the original entry determines semantic order.
/// </summary>
internal sealed class CombatPredictionHistory(PredictionTrace trace)
{
    private readonly List<CombatPredictionHistoryEntry> _entries = [];
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

    /// <summary>
    /// Aggregates risk through the latest supplied relevant entry.
    /// </summary>
    /// <remarks>Every supplied entry is expected to belong to this history; an empty sequence yields no risk.</remarks>
    public PredictionRisk GetRisk(IEnumerable<CombatPredictionHistoryEntry> entries)
    {
        CombatPredictionHistoryEntry? lastEntry = null;
        foreach (var entry in entries)
        {
            ValidateOwnership(entry);
            if (lastEntry is null || entry.Index > lastEntry.Index)
            {
                lastEntry = entry;
            }
        }

        return lastEntry is null
            ? PredictionRisk.None
            : GetRiskThrough(lastEntry.Index);
    }

    /// <summary>
    /// Aggregates risk through the current end of the timeline.
    /// </summary>
    public PredictionRisk GetCurrentRisk()
    {
        return _entries.Count == 0 ? PredictionRisk.None : GetRiskThrough(_entries.Count - 1);
    }

    public void RecordRisk(PredictionRiskReason reason)
    {
        Record(new CombatPredictionRiskEntry { Reason = reason });
    }

    public void CardAfflicted(PredictedCard card, AfflictionModel affliction)
    {
        Record(new CombatPredictionCardAfflictedEntry
        {
            Card = card.Clone(),
            Affliction = affliction
        });
    }

    public CombatPredictionCardDrawnEntry CardDrawn(PredictedCard card, bool fromHandDraw)
    {
        return Record(new CombatPredictionCardDrawnEntry
        {
            Card = card.Clone(),
            FromHandDraw = fromHandDraw
        });
    }

    public void CardDrawResolved(CombatPredictionCardDrawnEntry originalEntry, PredictedCard card)
    {
        Complete(originalEntry, new CombatPredictionCardDrawResolvedEntry
        {
            OriginalEntry = originalEntry,
            Card = card.Clone()
        });
    }

    public void CardCostsRandomized(IReadOnlyList<PredictedCard> cards)
    {
        Record(new CombatPredictionCardCostsRandomizedEntry { Cards = SnapshotCards(cards) });
    }

    public void CardsSelected(IReadOnlyList<PredictedCard> cards)
    {
        Record(new CombatPredictionCardsSelectedEntry { Cards = SnapshotCards(cards) });
    }

    public void CardPlayStarted(PredictedCard card, CardPlay cardPlay)
    {
        Record(new CombatPredictionCardPlayStartedEntry
        {
            Card = card,
            CardPlay = cardPlay
        });
    }

    public void CardPlayFinished(PredictedCard card, CardPlay cardPlay, bool wasEthereal)
    {
        Record(new CombatPredictionCardPlayFinishedEntry
        {
            Card = card,
            CardPlay = cardPlay,
            WasEthereal = wasEthereal
        });
    }

    public CombatPredictionCardGeneratedEntry CardGenerated(
        PredictedCard card,
        CardGenerationResultKind resultKind)
    {
        return Record(new CombatPredictionCardGeneratedEntry
        {
            Card = card.Clone(),
            ResultKind = resultKind
        });
    }

    public void CardGenerationResolved(CombatPredictionCardGeneratedEntry originalEntry, PredictedCard card)
    {
        Complete(originalEntry, new CombatPredictionCardGenerationResolvedEntry
        {
            OriginalEntry = originalEntry,
            Card = card.Clone()
        });
    }

    public void CardGenerationOptions(IReadOnlyList<PredictedCard> cards)
    {
        Record(new CombatPredictionCardGenerationOptionsEntry { Cards = SnapshotCards(cards) });
    }

    public void AutoPlayFromDrawPile(PredictedCard card)
    {
        Record(new CombatPredictionAutoPlayFromDrawPileEntry { Card = card.Clone() });
    }

    public void PotionGenerated(PotionModel potion)
    {
        Record(new CombatPredictionPotionGeneratedEntry { Potion = potion });
    }

    public void CreatureAttacked(
        Creature attacker,
        IReadOnlyList<DamageResult> hitResults)
    {
        Record(new CombatPredictionCreatureAttackedEntry
        {
            Attacker = attacker,
            HitResults = hitResults
        });
    }

    public void EnergySpent(Player player, int amount)
    {
        Record(new CombatPredictionEnergySpentEntry
        {
            Player = player,
            Amount = amount
        });
    }

    public void StarsModified(Player player, int amount)
    {
        Record(new CombatPredictionStarsModifiedEntry
        {
            Player = player,
            Amount = amount
        });
    }

    public void DamageReceived(Creature receiver, Creature? dealer, DamageResult result, PredictedCard? cardSource)
    {
        Record(new CombatPredictionDamageReceivedEntry
        {
            Receiver = receiver,
            Result = result,
            Dealer = dealer,
            CardSource = cardSource
        });
    }

    public void OrbChanneled(OrbModel orb)
    {
        Record(new CombatPredictionOrbChanneledEntry { Orb = orb });
    }

    /// <summary>
    /// Returns the resolution paired with one exact deferred started-entry instance.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the entry is unresolved, belongs to another history, or has a different resolution type.
    /// </exception>
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
        entry.Index = _entries.Count;
        entry.Trace = trace.Current;
        _entries.Add(entry);
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

    private CombatPredictionRisk GetRiskThrough(int boundaryIndex)
    {
        return new CombatPredictionRisk([.. _entries
            .Take(boundaryIndex + 1)
            .OfType<CombatPredictionRiskEntry>()]);
    }
}
