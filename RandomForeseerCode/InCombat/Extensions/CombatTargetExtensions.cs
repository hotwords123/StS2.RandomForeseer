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
    }
}
