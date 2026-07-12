using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models.Orbs;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Orbs;

internal static class PlasmaOrbMirrors
{
    // Mirrors PlasmaOrb.Passive without VFX/SFX or waits.
    public static void Passive(PlasmaOrb orb, OrbPassiveMirrorContext context)
    {
        context.Simulator.GainEnergy(orb.Owner, orb.PassiveVal);
    }

    // Mirrors PlasmaOrb.Evoke without VFX/SFX or waits.
    public static IReadOnlyList<Creature> Evoke(PlasmaOrb orb, OrbMirrorContext context)
    {
        context.Simulator.GainEnergy(orb.Owner, orb.EvokeVal);
        return [orb.Owner.Creature];
    }
}
