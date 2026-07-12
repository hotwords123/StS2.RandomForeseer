using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models.Orbs;
using MegaCrit.Sts2.Core.ValueProps;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Orbs;

internal static class DarkOrbMirrors
{
    // Mirrors DarkOrb.BeforeTurnEndOrbTrigger by forwarding through OrbModel.TriggerPassive.
    public static void BeforeTurnEndOrbTrigger(DarkOrb orb, OrbMirrorContext context)
    {
        context.Simulator.TriggerOrbPassive(orb, target: null);
    }

    // Mirrors DarkOrb.Passive by mutating only the simulator's cloned orb.
    public static void Passive(DarkOrb orb, OrbPassiveMirrorContext context)
    {
        orb._evokeVal += orb.PassiveVal;
    }

    // Mirrors DarkOrb.Evoke without VFX/SFX or waits.
    public static IReadOnlyList<Creature> Evoke(DarkOrb orb, OrbMirrorContext context)
    {
        var target = context.State.HittableEnemies
            .MinBy(creature => context.State.GetCreature(creature).CurrentHp);
        if (target is null)
        {
            return [];
        }

        IReadOnlyList<Creature> targets = [target];
        context.Simulator.Damage(targets, orb.EvokeVal, ValueProp.Unpowered, orb.Owner.Creature);
        return targets;
    }
}
