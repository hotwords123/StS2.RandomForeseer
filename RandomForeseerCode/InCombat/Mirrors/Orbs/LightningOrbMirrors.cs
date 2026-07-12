using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models.Orbs;
using MegaCrit.Sts2.Core.ValueProps;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Orbs;

internal static class LightningOrbMirrors
{
    // Mirrors LightningOrb.BeforeTurnEndOrbTrigger by forwarding through OrbModel.TriggerPassive.
    public static void BeforeTurnEndOrbTrigger(LightningOrb orb, OrbMirrorContext context)
    {
        context.Simulator.TriggerOrbPassive(orb, target: null);
    }

    // Mirrors LightningOrb.Passive without VFX/SFX or waits.
    public static void Passive(LightningOrb orb, OrbPassiveMirrorContext context)
    {
        Damage(orb, context, orb.PassiveVal, context.Target);
    }

    // Mirrors LightningOrb.Evoke without VFX/SFX or waits.
    public static IReadOnlyList<Creature> Evoke(LightningOrb orb, OrbMirrorContext context)
    {
        return Damage(orb, context, orb.EvokeVal, target: null);
    }

    private static IReadOnlyList<Creature> Damage(
        LightningOrb orb,
        OrbMirrorContext context,
        decimal value,
        Creature? target)
    {
        var candidates = context.State.GetOpponentsOf(orb.Owner.Creature)
            .Where(creature => context.State.GetCreature(creature).IsHittable)
            .ToList();
        if (candidates.Count == 0)
        {
            return [];
        }

        target ??= context.Rng.CombatTargets.NextItem(candidates);
        if (target is null)
        {
            return [];
        }

        IReadOnlyList<Creature> targets = [target];
        context.Simulator.Damage(targets, value, ValueProp.Unpowered, orb.Owner.Creature);
        return targets;
    }
}
