using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using RandomForeseer.RandomForeseerCode.Common;

namespace RandomForeseer.RandomForeseerCode.InCombat.Simulation;

internal sealed partial class CombatPredictionSimulator
{
    private readonly List<CombatPredictionDamageHistoryEntry> _damageHistory = [];
    private readonly List<CombatPredictionOrbChanneledHistoryEntry> _orbChanneledHistory = [];
    private readonly List<CombatPredictionCardDrawnHistoryEntry> _cardDrawnHistory = [];

    public IReadOnlyList<CombatPredictionDamageHistoryEntry> DamageHistory => _damageHistory;

    public IReadOnlyList<CombatPredictionOrbChanneledHistoryEntry> OrbChanneledHistory => _orbChanneledHistory;

    public IReadOnlyList<CombatPredictionCardDrawnHistoryEntry> CardDrawnHistory => _cardDrawnHistory;

    private void RecordDamageHistory(DamageResult result, Creature? dealer, PredictedCard? cardSource)
    {
        _damageHistory.Add(new CombatPredictionDamageHistoryEntry(
            result.Receiver,
            result,
            dealer,
            cardSource,
            _sourceStack.Current ?? cardSource?.Original));
    }

    private void RecordOrbChanneledHistory(OrbModel orb)
    {
        _orbChanneledHistory.Add(new CombatPredictionOrbChanneledHistoryEntry(orb));
    }

    private void RecordCardDrawnHistory(Player player, PredictedCard card)
    {
        _cardDrawnHistory.Add(new CombatPredictionCardDrawnHistoryEntry(player, card));
    }
}

internal sealed record CombatPredictionDamageHistoryEntry(
    Creature Receiver,
    DamageResult Result,
    Creature? Dealer,
    PredictedCard? CardSource,
    AbstractModel? SourceModel);

internal sealed record CombatPredictionOrbChanneledHistoryEntry(OrbModel Orb);

internal sealed record CombatPredictionCardDrawnHistoryEntry(Player Player, PredictedCard Card);
