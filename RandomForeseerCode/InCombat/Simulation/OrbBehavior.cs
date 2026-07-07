using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Orbs;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace RandomForeseer.RandomForeseerCode.InCombat.Simulation;

internal sealed class OrbBehavior(CombatPredictionSimulator simulator)
{
    public IReadOnlyList<Creature> Evoke(OrbModel orb)
    {
        using (simulator.PushSource(orb))
        {
            return orb switch
            {
                LightningOrb lightningOrb => LightningOrbEvoke(lightningOrb),
                FrostOrb frostOrb => FrostOrbEvoke(frostOrb),
                DarkOrb darkOrb => DarkOrbEvoke(darkOrb),
                GlassOrb glassOrb => GlassOrbEvoke(glassOrb),
                PlasmaOrb plasmaOrb => PlasmaOrbEvoke(plasmaOrb),
                _ => UnsupportedOrbEvoke(orb)
            };
        }
    }

    public void Passive(OrbModel orb, Creature? target = null)
    {
        using (simulator.PushSource(orb))
        {
            switch (orb)
            {
                case LightningOrb lightningOrb:
                    LightningOrbPassive(lightningOrb, target);
                    break;
                case FrostOrb frostOrb:
                    FrostOrbPassive(frostOrb);
                    break;
                case DarkOrb darkOrb:
                    DarkOrbPassive(darkOrb);
                    break;
                case GlassOrb glassOrb:
                    GlassOrbPassive(glassOrb);
                    break;
                case PlasmaOrb plasmaOrb:
                    PlasmaOrbPassive(plasmaOrb);
                    break;
                default:
                    simulator.MarkCurrentSourceRisky();
                    break;
            }
        }
    }

    // Mirrors OrbModel.BeforeTurnEndOrbTrigger for vanilla orbs.
    public void BeforeTurnEndTrigger(OrbModel orb)
    {
        using (simulator.PushSource(orb))
        {
            switch (orb)
            {
                case LightningOrb lightningOrb:
                    LightningOrbPassive(lightningOrb, target: null);
                    break;
                case FrostOrb frostOrb:
                    FrostOrbPassive(frostOrb);
                    break;
                case DarkOrb darkOrb:
                    DarkOrbPassive(darkOrb);
                    break;
                case GlassOrb glassOrb:
                    GlassOrbPassive(glassOrb);
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

    private void LightningOrbPassive(LightningOrb orb, Creature? target)
    {
        LightningOrbDamage(orb, orb.PassiveVal, target);
    }

    private IReadOnlyList<Creature> LightningOrbEvoke(LightningOrb orb)
    {
        return LightningOrbDamage(orb, orb.EvokeVal, target: null);
    }

    private IReadOnlyList<Creature> LightningOrbDamage(LightningOrb orb, decimal value, Creature? target)
    {
        var candidates = simulator.State.GetOpponentsOf(orb.Owner.Creature)
            .Where(creature => simulator.State.GetCreature(creature).IsHittable)
            .ToArray();
        if (candidates.Length == 0)
        {
            return [];
        }

        target ??= simulator.Rng.CombatTargets.NextItem(candidates);
        if (target == null)
        {
            return [];
        }

        IReadOnlyList<Creature> targets = [target];
        simulator.Damage(targets, value, ValueProp.Unpowered, orb.Owner.Creature);
        return targets;
    }

    private void FrostOrbPassive(FrostOrb orb)
    {
        FrostOrbBlock(orb, orb.PassiveVal);
    }

    private IReadOnlyList<Creature> FrostOrbEvoke(FrostOrb orb)
    {
        return FrostOrbBlock(orb, orb.EvokeVal);
    }

    private IReadOnlyList<Creature> FrostOrbBlock(FrostOrb orb, decimal value)
    {
        simulator.GainBlock(orb.Owner.Creature, value, ValueProp.Unpowered);

        if (!orb.Owner.Creature.HasPower<HibernatePower>())
        {
            return [orb.Owner.Creature];
        }

        // StS2 v0.108.0 made Frost orbs grant the same block to all players while
        // Hibernate is on the owner; vanilla still grants the owner block first.
        var allPlayers = simulator.State.CombatState.Players;

        foreach (var player in allPlayers)
        {
            if (player != orb.Owner)
            {
                simulator.GainBlock(player.Creature, value, ValueProp.Unpowered);
            }
        }

        return allPlayers.Select(player => player.Creature).ToArray();
    }

    private void DarkOrbPassive(DarkOrb orb)
    {
        // We can modify the orb's evoke value because the simulator uses a cloned orb queue.
        // The real orb queue is not mutated.
        orb._evokeVal += orb.PassiveVal;
    }

    private IReadOnlyList<Creature> DarkOrbEvoke(DarkOrb orb)
    {
        var target = simulator.State.HittableEnemies
            .MinBy(creature => simulator.State.GetCreature(creature).CurrentHp);
        if (target == null)
        {
            return [];
        }

        IReadOnlyList<Creature> targets = [target];
        simulator.Damage(targets, orb.EvokeVal, ValueProp.Unpowered, orb.Owner.Creature);
        return targets;
    }

    private void GlassOrbPassive(GlassOrb orb)
    {
        var passiveVal = orb.PassiveVal;
        if (passiveVal <= 0m)
        {
            return;
        }

        // We can modify the orb's passive value because the simulator uses a cloned orb queue.
        // The real orb queue is not mutated.
        orb._passiveVal = Math.Max(0m, orb._passiveVal - 1m);
        GlassOrbDamage(orb, passiveVal);
    }

    private IReadOnlyList<Creature> GlassOrbEvoke(GlassOrb orb)
    {
        if (orb.EvokeVal <= 0m)
        {
            return [];
        }

        return GlassOrbDamage(orb, orb.EvokeVal);
    }

    private IReadOnlyList<Creature> GlassOrbDamage(GlassOrb orb, decimal value)
    {
        var targets = simulator.State.HittableEnemies;
        simulator.Damage(targets, value, ValueProp.Unpowered, orb.Owner.Creature);
        return targets;
    }

    private void PlasmaOrbPassive(PlasmaOrb orb)
    {
        simulator.GainEnergy(orb.Owner, orb.PassiveVal);
    }

    private IReadOnlyList<Creature> PlasmaOrbEvoke(PlasmaOrb orb)
    {
        simulator.GainEnergy(orb.Owner, orb.EvokeVal);
        return [orb.Owner.Creature];
    }

    private IReadOnlyList<Creature> UnsupportedOrbEvoke(OrbModel orb)
    {
        simulator.MarkCurrentSourceRisky();
        return [];
    }
}
