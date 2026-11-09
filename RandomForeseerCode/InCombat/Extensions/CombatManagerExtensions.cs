using MegaCrit.Sts2.Core.Combat;

namespace RandomForeseer.InCombat.Extensions;

internal static class CombatManagerExtensions
{
    extension(CombatManager combatManager)
    {
        public CombatTurnState? LiveTurnState => combatManager._turnState is { IsInProgress: true } turnState
            ? turnState
            : null;

        public CombatState? LiveCombatState => combatManager.LiveTurnState?.State;
    }
}
