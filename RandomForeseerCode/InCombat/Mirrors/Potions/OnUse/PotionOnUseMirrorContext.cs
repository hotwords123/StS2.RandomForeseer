using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace RandomForeseer.InCombat.Mirrors.Potions.OnUse;

/// <summary>
/// Provides simulator-owned state and the resolved source/target pair to one potion <see cref="PotionModel.OnUse"/>
/// mirror.
/// </summary>
internal sealed class PotionOnUseMirrorContext : CombatMirrorContext<PotionModel>
{
    /// <summary>The exact live mutable potion whose behavior is being mirrored.</summary>
    public required PotionModel Potion { get; init; }

    /// <summary>The validated potion target, or <see langword="null"/> for a valid non-creature target mode.</summary>
    public required Creature? Target { get; init; }

    /// <summary>Gets the validated target as a player or throws when a handler uses this helper for another target kind.</summary>
    public Player TargetPlayer => Target?.Player
        ?? throw new InvalidOperationException("This potion mirror requires a player target.");
}
