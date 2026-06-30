using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Orbs;
using MegaCrit.Sts2.Core.ValueProps;

namespace RandomForeseer.RandomForeseerCode.InCombat.Simulation;

internal static class OrbBehavior
{
    public static IReadOnlyList<Creature> Evoke(CombatPredictionSimulator simulator, OrbModel orb)
    {
        using (simulator.PushSource(orb))
        {
            return orb switch
            {
                LightningOrb lightningOrb => LightningOrbEvoke(simulator, lightningOrb),
                FrostOrb frostOrb => FrostOrbEvoke(simulator, frostOrb),
                DarkOrb darkOrb => DarkOrbEvoke(simulator, darkOrb),
                GlassOrb glassOrb => GlassOrbEvoke(simulator, glassOrb),
                PlasmaOrb plasmaOrb => PlasmaOrbEvoke(simulator, plasmaOrb),
                _ => UnsupportedOrbEvoke(simulator, orb)
            };
        }
    }

    public static void Passive(CombatPredictionSimulator simulator, OrbModel orb, Creature? target = null)
    {
        using (simulator.PushSource(orb))
        {
            switch (orb)
            {
                case LightningOrb lightningOrb:
                    LightningOrbPassive(simulator, lightningOrb, target);
                    break;
                case FrostOrb frostOrb:
                    FrostOrbPassive(simulator, frostOrb);
                    break;
                case DarkOrb darkOrb:
                    DarkOrbPassive(simulator, darkOrb);
                    break;
                case GlassOrb glassOrb:
                    GlassOrbPassive(simulator, glassOrb);
                    break;
                case PlasmaOrb plasmaOrb:
                    PlasmaOrbPassive(simulator, plasmaOrb);
                    break;
                default:
                    simulator.MarkCurrentSourceRisky();
                    break;
            }
        }
    }

    // Mirrors OrbModel.BeforeTurnEndOrbTrigger for vanilla orbs.
    public static void BeforeTurnEndTrigger(CombatPredictionSimulator simulator, OrbModel orb)
    {
        using (simulator.PushSource(orb))
        {
            switch (orb)
            {
                case LightningOrb lightningOrb:
                    LightningOrbPassive(simulator, lightningOrb, target: null);
                    break;
                case FrostOrb frostOrb:
                    FrostOrbPassive(simulator, frostOrb);
                    break;
                case DarkOrb darkOrb:
                    DarkOrbPassive(simulator, darkOrb);
                    break;
                case GlassOrb glassOrb:
                    GlassOrbPassive(simulator, glassOrb);
                    break;
                case PlasmaOrb:
                    // PlasmaOrb does not trigger at end of turn; it only forwards to Passive after turn start.
                    break;
                default:
                    simulator.MarkCurrentSourceRisky();
                    break;
            }
        }
    }

    private static void LightningOrbPassive(CombatPredictionSimulator simulator, LightningOrb orb, Creature? target)
    {
        LightningOrbDamage(simulator, orb, orb.PassiveVal, target);
    }

    private static IReadOnlyList<Creature> LightningOrbEvoke(CombatPredictionSimulator simulator, LightningOrb orb)
    {
        return LightningOrbDamage(simulator, orb, orb.EvokeVal, target: null);
    }

    private static IReadOnlyList<Creature> LightningOrbDamage(
        CombatPredictionSimulator simulator,
        LightningOrb orb,
        decimal value,
        Creature? target)
    {
        var candidates = simulator.State.GetHittableOpponentsOf(orb.Owner.Creature);
        if (candidates.Count == 0)
        {
            return [];
        }

        target ??= simulator.Rng.CombatTargets.NextItem(candidates);
        if (target == null)
        {
            return [];
        }

        var targets = (IReadOnlyList<Creature>)[target];
        simulator.Damage(targets, value, ValueProp.Unpowered, orb.Owner.Creature);
        return targets;
    }

    private static void FrostOrbPassive(CombatPredictionSimulator simulator, FrostOrb orb)
    {
        simulator.GainBlock(orb.Owner.Creature, orb.PassiveVal, ValueProp.Unpowered);
    }

    private static IReadOnlyList<Creature> FrostOrbEvoke(CombatPredictionSimulator simulator, FrostOrb orb)
    {
        simulator.GainBlock(orb.Owner.Creature, orb.EvokeVal, ValueProp.Unpowered);
        return [orb.Owner.Creature];
    }

    private static void DarkOrbPassive(CombatPredictionSimulator simulator, DarkOrb orb)
    {
        // We can modify the orb's evoke value because the simulator uses a cloned orb queue.
        // The real orb queue is not mutated.
        orb._evokeVal += orb.PassiveVal;
    }

    private static IReadOnlyList<Creature> DarkOrbEvoke(CombatPredictionSimulator simulator, DarkOrb orb)
    {
        var target = simulator.State.GetHittableOpponentsOf(orb.Owner.Creature)
            .MinBy(creature => simulator.State.GetCreature(creature).CurrentHp);
        if (target == null)
        {
            return [];
        }

        var targets = (IReadOnlyList<Creature>)[target];
        simulator.Damage(targets, orb.EvokeVal, ValueProp.Unpowered, orb.Owner.Creature);
        return targets;
    }

    private static void GlassOrbPassive(CombatPredictionSimulator simulator, GlassOrb orb)
    {
        var passiveVal = orb.PassiveVal;
        if (passiveVal <= 0m)
        {
            return;
        }

        // We can modify the orb's passive value because the simulator uses a cloned orb queue.
        // The real orb queue is not mutated.
        orb._passiveVal = Math.Max(0m, orb._passiveVal - 1m);

        var targets = simulator.State.GetHittableOpponentsOf(orb.Owner.Creature);
        simulator.Damage(targets, passiveVal, ValueProp.Unpowered, orb.Owner.Creature);
    }

    private static IReadOnlyList<Creature> GlassOrbEvoke(CombatPredictionSimulator simulator, GlassOrb orb)
    {
        if (orb.EvokeVal <= 0m)
        {
            return [];
        }

        var targets = simulator.State.GetHittableOpponentsOf(orb.Owner.Creature);
        simulator.Damage(targets, orb.EvokeVal, ValueProp.Unpowered, orb.Owner.Creature);
        return targets;
    }

    private static void PlasmaOrbPassive(CombatPredictionSimulator simulator, PlasmaOrb orb)
    {
        // Energy gain is not modeled in the combat prediction simulator.
    }

    private static IReadOnlyList<Creature> PlasmaOrbEvoke(CombatPredictionSimulator simulator, PlasmaOrb orb)
    {
        // Energy gain is not modeled in the combat prediction simulator.
        return [orb.Owner.Creature];
    }

    private static IReadOnlyList<Creature> UnsupportedOrbEvoke(CombatPredictionSimulator simulator, OrbModel orb)
    {
        simulator.MarkCurrentSourceRisky();
        return [];
    }
}
