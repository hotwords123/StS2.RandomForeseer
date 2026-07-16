using System.Runtime.InteropServices;
using MegaCrit.Sts2.Core.Entities.Creatures;
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

        return GetRiskThrough(entries.Max(static entry => entry.Index));
    }

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

    public void CardsSelected(IReadOnlyList<PredictedCard> cards)
    {
        Record(new CombatPredictionCardsSelectedEntry { Cards = SnapshotCards(cards) });
    }

    public CombatPredictionCardGeneratedEntry CardGenerated(PredictedCard card)
    {
        return Record(new CombatPredictionCardGeneratedEntry { Card = card.Clone() });
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

    private PredictionRisk GetRiskThrough(int boundaryIndex)
    {
        List<AbstractModel> models = [];
        HashSet<ModelId> modelIds = [];
        HashSet<PredictionRiskReason> reasons = [];

        foreach (var riskEntry in _entries.Take(boundaryIndex + 1).OfType<CombatPredictionRiskEntry>())
        {
            reasons.Add(riskEntry.Reason);

            foreach (var frame in riskEntry.Trace?.Ancestors().Reverse() ?? [])
            {
                if (modelIds.Add(frame.Source.Id))
                {
                    models.Add(frame.Source);
                }
            }
        }

        return reasons.Count == 0
            ? PredictionRisk.None
            : new PredictionRisk(true, models, reasons);
    }
}
