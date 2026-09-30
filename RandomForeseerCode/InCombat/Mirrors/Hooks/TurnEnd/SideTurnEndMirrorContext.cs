using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Hooks.TurnEnd;

internal sealed class SideTurnEndMirrorContext : CombatMirrorContext
{
    public required CombatSide Side { get; init; }

    public required IReadOnlyList<Creature> Participants { get; init; }
}
