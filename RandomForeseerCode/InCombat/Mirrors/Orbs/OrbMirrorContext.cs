using MegaCrit.Sts2.Core.Entities.Creatures;

namespace RandomForeseer.InCombat.Mirrors.Orbs;

internal class OrbMirrorContext : CombatMirrorContext;

internal sealed class OrbPassiveMirrorContext : OrbMirrorContext
{
    public Creature? Target { get; init; }
}
