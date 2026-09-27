using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using RandomForeseer.RandomForeseerCode.Common;

namespace RandomForeseer.RandomForeseerCode.InCombat.Simulation;

/// <summary>Classifies how a generated card result is determined for projection policy.</summary>
internal enum CardGenerationResultKind
{
    /// <summary>The generated card identity or visible state depends on prediction RNG.</summary>
    Random,

    /// <summary>The result is deterministic but depends on runtime context that is useful to surface.</summary>
    Contextual,

    /// <summary>The source always produces an already-described card result that does not need a prediction tip.</summary>
    Fixed
}

/// <summary>
/// Represents one identity-based occurrence in a combat prediction timeline.
/// </summary>
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

/// <summary>
/// Mirrors <see cref="CardPlayStartedEntry"/>.
/// </summary>
internal sealed class CombatPredictionCardPlayStartedEntry : CombatPredictionHistoryEntry
{
    public required PredictedCard Card { get; init; }
    public required CardPlay CardPlay { get; init; }
}

/// <summary>
/// Mirrors <see cref="CardPlayFinishedEntry"/>.
/// </summary>
internal sealed class CombatPredictionCardPlayFinishedEntry : CombatPredictionHistoryEntry
{
    public required PredictedCard Card { get; init; }
    public required CardPlay CardPlay { get; init; }
    public required bool WasEthereal { get; init; }
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

internal sealed class CombatPredictionEnergySpentEntry : CombatPredictionHistoryEntry
{
    public required Player Player { get; init; }
    public required int Amount { get; init; }
}

internal sealed class CombatPredictionStarsModifiedEntry : CombatPredictionHistoryEntry
{
    public required Player Player { get; init; }
    public required int Amount { get; init; }
}

/// <summary>Records an orb-channel gameplay event.</summary>
internal sealed class CombatPredictionOrbChanneledEntry : CombatPredictionHistoryEntry
{
    public required OrbModel Orb { get; init; }
}

/// <summary>Records the gameplay occurrence of drawing a card.</summary>
internal sealed class CombatPredictionCardDrawnEntry : CombatPredictionHistoryEntry
{
    public required PredictedCard Card { get; init; }
    public required bool FromHandDraw { get; init; }
}

/// <summary>Stores the presentation state after a recorded draw finishes resolving.</summary>
internal sealed class CombatPredictionCardDrawResolvedEntry : CombatPredictionHistoryEntry
{
    public required CombatPredictionCardDrawnEntry OriginalEntry { get; init; }
    public required CardModel PreviewCard { get; init; }
}

/// <summary>Stores the final hand presentation after card costs are randomized.</summary>
internal sealed class CombatPredictionCardCostsRandomizedEntry : CombatPredictionHistoryEntry
{
    public required IReadOnlyList<CardModel> PreviewCards { get; init; }
}

/// <summary>Records selected source-card identities alongside their presentation snapshots.</summary>
internal sealed class CombatPredictionCardsSelectedEntry : CombatPredictionHistoryEntry
{
    public required IReadOnlyList<CardModel> SourceCards { get; init; }
    public required IReadOnlyList<CardModel> PreviewCards { get; init; }
}

/// <summary>Records the gameplay occurrence of generating a card.</summary>
internal sealed class CombatPredictionCardGeneratedEntry : CombatPredictionHistoryEntry
{
    public required PredictedCard Card { get; init; }
    public required CardGenerationResultKind ResultKind { get; init; }
}

/// <summary>
/// Stores the generated-card presentation snapshot after resolution; <see cref="PreviewCard"/> is <see langword="null"/>
/// when the card was not added to a pile.
/// </summary>
internal sealed class CombatPredictionCardGenerationResolvedEntry : CombatPredictionHistoryEntry
{
    public required CombatPredictionCardGeneratedEntry OriginalEntry { get; init; }
    public required CardModel? PreviewCard { get; init; }
}

/// <summary>Stores generated card options for presentation.</summary>
internal sealed class CombatPredictionCardGenerationOptionsEntry : CombatPredictionHistoryEntry
{
    public required IReadOnlyList<CardModel> PreviewCards { get; init; }
}

internal sealed class CombatPredictionCardAfflictedEntry : CombatPredictionHistoryEntry
{
    public required PredictedCard Card { get; init; }
    public required AfflictionModel Affliction { get; init; }
}

/// <summary>Records an auto-play gameplay event.</summary>
internal sealed class CombatPredictionAutoPlayFromDrawPileEntry : CombatPredictionHistoryEntry
{
    public required PredictedCard Card { get; init; }
}

/// <summary>Stores a generated potion for presentation.</summary>
internal sealed class CombatPredictionPotionGeneratedEntry : CombatPredictionHistoryEntry
{
    public required PotionModel PreviewPotion { get; init; }
}
