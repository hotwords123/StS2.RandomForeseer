using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models.Orbs;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Orbs;

internal static class FrostOrbMirrors
{
    // Mirrors FrostOrb.BeforeTurnEndOrbTrigger by forwarding through OrbModel.TriggerPassive.
    public static void BeforeTurnEndOrbTrigger(FrostOrb orb, OrbMirrorContext context)
    {
        context.Simulator.TriggerOrbPassive(orb, target: null);
    }

    // Mirrors FrostOrb.Passive without VFX/SFX or waits.
    public static void Passive(FrostOrb orb, OrbPassiveMirrorContext context)
    {
        Block(orb, context, orb.PassiveVal);
    }

    // Mirrors FrostOrb.Evoke without VFX/SFX or waits.
    public static IReadOnlyList<Creature> Evoke(FrostOrb orb, OrbMirrorContext context)
    {
        return Block(orb, context, orb.EvokeVal);
    }

    private static IReadOnlyList<Creature> Block(FrostOrb orb, OrbMirrorContext context, decimal value)
    {
        context.Simulator.GainBlock(orb.Owner.Creature, value, ValueProp.Unpowered);

        if (!orb.Owner.Creature.HasPower<HibernatePower>())
        {
            return [orb.Owner.Creature];
        }

        // StS2 v0.108.0 grants the owner block first, then the same block to all other players.
        var allPlayers = context.State.CombatState.Players;
        foreach (var player in allPlayers)
        {
            if (player != orb.Owner)
            {
                context.Simulator.GainBlock(player.Creature, value, ValueProp.Unpowered);
            }
        }

        return [.. allPlayers.Select(player => player.Creature)];
    }
}
