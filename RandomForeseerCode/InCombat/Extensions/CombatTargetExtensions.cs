using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;

namespace RandomForeseer.RandomForeseerCode.InCombat.Extensions;

internal static class CombatTargetExtensions
{
    extension(CardModel card)
    {
        /// <summary>
        /// Returns all valid targets that can be manually selected for a given card.
        /// Does not handle cards that does not require target selection (e.g. cards that target self or all enemies).
        /// </summary>
        public IReadOnlyList<Creature> GetValidTargets()
        {
            if (card.Owner.Creature.CombatState is not { } combatState)
            {
                return [];
            }

            return card.TargetType switch
            {
                TargetType.AnyEnemy =>
                    [.. combatState.Enemies.Where(creature => creature.IsAlive)],
                TargetType.AnyPlayer =>
                    [.. combatState.PlayerCreatures.Where(creature => creature.IsAlive)],
                TargetType.AnyAlly =>
                    [.. combatState.PlayerCreatures.Where(creature => creature != card.Owner.Creature && creature.IsAlive)],
                _ => [],
            };
        }

        /// <summary>
        /// Attempts to resolve a target for the given card.
        /// If a target is provided, returns whether it is valid without replacing it.
        /// If no target is required, returns true. Otherwise, uses the only valid manual target when one exists.
        /// Returns false if no valid target can be resolved.
        /// </summary>
        public bool TryResolveTarget(ref Creature? target)
        {
            if (card.IsValidTarget(target))
            {
                return true;
            }

            if (target is not null || card.GetValidTargets() is not [var validTarget])
            {
                return false;
            }

            target = validTarget;
            return true;
        }
    }
}
