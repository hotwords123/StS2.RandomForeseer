using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;

namespace RandomForeseer.RandomForeseerCode.InCombat;

internal static class CombatTargetExtensions
{
    // Returns all valid targets that can be manually selected for a given card.
    // Does not handle cards that does not require target selection (e.g. cards that target self or all enemies).
    public static IReadOnlyList<Creature> GetValidManualCardTargets(this ICombatState combatState, CardModel card)
    {
        return combatState.GetValidManualTargets(card.Owner.Creature, card.TargetType);
    }

    // Returns all valid targets that can be manually selected for a given player action.
    // Does not handle the case where the action does not require target selection (e.g. targeting self or all enemies).
    public static IReadOnlyList<Creature> GetValidManualTargets(this ICombatState combatState, Creature self, TargetType targetType)
    {
        return targetType switch
        {
            TargetType.AnyEnemy =>
                [.. combatState.Enemies.Where(creature => creature.IsAlive)],
            TargetType.AnyPlayer =>
                [.. combatState.PlayerCreatures.Where(creature => creature.IsAlive)],
            TargetType.AnyAlly =>
                [.. combatState.PlayerCreatures.Where(creature => creature != self && creature.IsAlive)],
            _ => [],
        };
    }

    // Attempts to resolve a target for the given card.
    // If a target is provided, returns whether it is valid without replacing it.
    // If no target is required, returns true. Otherwise, uses the only valid manual target when one exists.
    // Returns false if no valid target can be resolved.
    public static bool TryResolveTarget(this CardModel card, ref Creature? target)
    {
        if (card.IsValidTarget(target))
        {
            return true;
        }

        if (target is not null)
        {
            return false;
        }

        if (card.Owner.Creature.CombatState?.GetValidManualCardTargets(card) is [var validTarget])
        {
            target = validTarget;
            return true;
        }

        return false;
    }
}
