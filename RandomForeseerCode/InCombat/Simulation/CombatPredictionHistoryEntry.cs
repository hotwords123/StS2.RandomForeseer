using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using RandomForeseer.RandomForeseerCode.Common;

namespace RandomForeseer.RandomForeseerCode.InCombat.Simulation;

internal abstract class CombatPredictionHistoryEntry
{
    // The property setters are provided for the sake of object initializers; they should not be modified
    // after construction.
    public int Index { get; set; }
    public PredictionTraceFrame? Trace { get; set; }
}

internal sealed class CombatPredictionRiskEntry : CombatPredictionHistoryEntry
{
    public required PredictionRiskReason Reason { get; init; }
}

internal sealed class CombatPredictionDamageReceivedEntry : CombatPredictionHistoryEntry
{
    public required Creature Receiver { get; init; }
    public required DamageResult Result { get; init; }
    public required Creature? Dealer { get; init; }
    public required PredictedCard? CardSource { get; init; }
}

internal sealed class CombatPredictionCreatureAttackedEntry : CombatPredictionHistoryEntry
{
    public required Creature Attacker { get; init; }
    public required IReadOnlyList<DamageResult> HitResults { get; init; }
}

internal sealed class CombatPredictionOrbChanneledEntry : CombatPredictionHistoryEntry
{
    public required OrbModel Orb { get; init; }
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
}

internal sealed class CombatPredictionCardGeneratedEntry : CombatPredictionHistoryEntry
{
    public required PredictedCard Card { get; init; }
}

internal sealed class CombatPredictionCardGenerationResolvedEntry : CombatPredictionHistoryEntry
{
    public required CombatPredictionCardGeneratedEntry OriginalEntry { get; init; }
    public required PredictedCard Card { get; init; }
}

internal sealed class CombatPredictionCardGenerationOptionsEntry : CombatPredictionHistoryEntry
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
}

internal sealed class CombatPredictionPotionGeneratedEntry : CombatPredictionHistoryEntry
{
    public required PotionModel Potion { get; init; }
}
