using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models.Orbs;
using MegaCrit.Sts2.Core.ValueProps;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Orbs;

internal static class GlassOrbMirrors
{
    // Mirrors GlassOrb.BeforeTurnEndOrbTrigger by forwarding through OrbModel.TriggerPassive.
    public static void BeforeTurnEndOrbTrigger(GlassOrb orb, OrbMirrorContext context)
    {
        context.Simulator.TriggerOrbPassive(orb, target: null);
    }

    // Mirrors GlassOrb.Passive by mutating only the simulator's cloned orb.
    public static void Passive(GlassOrb orb, OrbPassiveMirrorContext context)
    {
        var passiveVal = orb.PassiveVal;
        if (passiveVal <= 0m)
        {
            return;
        }

        orb._passiveVal = Math.Max(0m, orb._passiveVal - 1m);
        Damage(orb, context, passiveVal);
    }

    // Mirrors GlassOrb.Evoke without VFX/SFX or waits.
    public static IReadOnlyList<Creature> Evoke(GlassOrb orb, OrbMirrorContext context)
    {
        return orb.EvokeVal <= 0m ? [] : Damage(orb, context, orb.EvokeVal);
    }

    private static IReadOnlyList<Creature> Damage(GlassOrb orb, OrbMirrorContext context, decimal value)
    {
        var targets = context.State.HittableEnemies;
        context.Simulator.Damage(targets, value, ValueProp.Unpowered, orb.Owner.Creature);
        return targets;
    }
}
