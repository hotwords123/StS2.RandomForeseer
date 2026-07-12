using MegaCrit.Sts2.Core.Entities.Creatures;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Orbs;

internal class OrbMirrorContext : CombatPredictionMirrorContext;

internal sealed class OrbPassiveMirrorContext : OrbMirrorContext
{
    public Creature? Target { get; init; }
}
