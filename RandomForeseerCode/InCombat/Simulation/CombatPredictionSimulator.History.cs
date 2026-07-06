using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using RandomForeseer.RandomForeseerCode.Common;

namespace RandomForeseer.RandomForeseerCode.InCombat.Simulation;

internal sealed partial class CombatPredictionSimulator
{
    private readonly List<CombatPredictionDamageHistoryEntry> _damageHistory = [];
    private readonly List<CombatPredictionAttackHistoryEntry> _attackHistory = [];
    private readonly List<CombatPredictionOrbChanneledHistoryEntry> _orbChanneledHistory = [];
    private readonly List<CombatPredictionCardDrawnHistoryEntry> _cardDrawnHistory = [];
    private readonly List<CombatPredictionCardAfflictedHistoryEntry> _cardAfflictedHistory = [];

    public IReadOnlyList<CombatPredictionDamageHistoryEntry> DamageHistory => _damageHistory;

    public IReadOnlyList<CombatPredictionAttackHistoryEntry> AttackHistory => _attackHistory;

    public IReadOnlyList<CombatPredictionOrbChanneledHistoryEntry> OrbChanneledHistory => _orbChanneledHistory;

    public IReadOnlyList<CombatPredictionCardDrawnHistoryEntry> CardDrawnHistory => _cardDrawnHistory;

    public IReadOnlyList<CombatPredictionCardAfflictedHistoryEntry> CardAfflictedHistory => _cardAfflictedHistory;

    private void RecordDamageHistory(DamageResult result, Creature? dealer, PredictedCard? cardSource)
    {
        _damageHistory.Add(new CombatPredictionDamageHistoryEntry(
            result.Receiver,
            result,
            dealer,
            cardSource,
            _sourceStack.Current ?? cardSource?.Original));
    }

    private void RecordAttackHistory(
        Creature attacker,
        AbstractModel? source,
        IReadOnlyList<DamageResult> hitResults)
    {
        _attackHistory.Add(new CombatPredictionAttackHistoryEntry(attacker, source, hitResults));
    }

    private void RecordOrbChanneledHistory(OrbModel orb)
    {
        _orbChanneledHistory.Add(new CombatPredictionOrbChanneledHistoryEntry(orb));
    }

    private void RecordCardDrawnHistory(PredictedCard card, bool fromHandDraw)
    {
        _cardDrawnHistory.Add(new CombatPredictionCardDrawnHistoryEntry(card, fromHandDraw));
    }

    private void RecordCardAfflictedHistory(PredictedCard card, AfflictionModel affliction)
    {
        _cardAfflictedHistory.Add(new CombatPredictionCardAfflictedHistoryEntry(card, affliction));
    }
}

internal sealed record CombatPredictionDamageHistoryEntry(
    Creature Receiver,
    DamageResult Result,
    Creature? Dealer,
    PredictedCard? CardSource,
    AbstractModel? SourceModel);

internal sealed record CombatPredictionAttackHistoryEntry(
    Creature Attacker,
    AbstractModel? SourceModel,
    IReadOnlyList<DamageResult> HitResults);

internal sealed record CombatPredictionOrbChanneledHistoryEntry(OrbModel Orb);

internal sealed record CombatPredictionCardDrawnHistoryEntry(
    PredictedCard Card,
    bool FromHandDraw);

internal sealed record CombatPredictionCardAfflictedHistoryEntry(
    PredictedCard Card,
    AfflictionModel Affliction);
