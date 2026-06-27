using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Orbs;
using MegaCrit.Sts2.Core.ValueProps;
using RandomForeseer.InCombat.Hooks;

namespace RandomForeseer.InCombat.Simulation;

internal sealed partial class CombatPredictionSimulator
{
    // Mirrors OrbQueue.BeforeTurnEnd without waits, real queue mutation, or async hook execution.
    private void SimulateOrbQueueBeforeTurnEnd(Player player)
    {
        var orbQueue = State.GetPlayerCombatState(player).OrbQueue;
        foreach (var orb in orbQueue.Orbs.ToList())
        {
            if (player.Creature.CombatState == null)
            {
                return;
            }

            var passiveCountContext = new OrbPassiveCountHookContext
            {
                Simulator = this,
                Orb = orb
            };
            var triggerCount = OrbPassiveCountHooks.ModifyOrbPassiveTriggerCount(passiveCountContext);

            for (var i = 0; i < triggerCount; i++)
            {
                OrbPassive(orb);
            }
        }
    }

    // Mirrors OrbCmd.Channel<T> without mutating the real orb queue.
    public OrbModel? OrbChannel<T>(Player player) where T : OrbModel
    {
        return OrbChannel(player, ModelDb.Orb<T>().ToMutable());
    }

    // Mirrors Chaos' OrbModel.GetRandomOrb(CombatOrbGeneration).ToMutable() channel path.
    public OrbModel? OrbChannelRandom(Player player)
    {
        return OrbChannel(player, OrbModel.GetRandomOrb(Rng.CombatOrbGeneration).ToMutable());
    }

    // Mirrors OrbCmd.Channel without VFX/SFX, waits, real queue mutation, or async hook execution.
    public OrbModel? OrbChannel(Player player, OrbModel orb)
    {
        var orbQueue = State.GetPlayerCombatState(player).OrbQueue;
        if (player.Character.BaseOrbSlotCount == 0 && orbQueue.Capacity == 0)
        {
            orbQueue.AddCapacity(1);
        }

        orb.AssertMutable();
        orb.Owner = player;

        if (orbQueue.Orbs.Count >= orbQueue.Capacity)
        {
            OrbEvokeNext(player);
        }

        if (!orbQueue.TryEnqueue(orb))
        {
            return null;
        }

        RecordOrbChanneledHistory(orb);
        AfterOrbChanneled(player, orb);
        return orb;
    }

    // Mirrors OrbCmd.EvokeNext without mutating the real orb queue.
    public void OrbEvokeNext(Player player, bool dequeue = true)
    {
        var orb = State.GetPlayerCombatState(player).OrbQueue.Orbs.FirstOrDefault();
        if (orb != null)
        {
            OrbEvoke(player, orb, dequeue);
        }
    }

    // Mirrors OrbCmd.Evoke without VFX/SFX, choice-context model stack updates, or real queue mutation.
    public void OrbEvoke(Player player, OrbModel evokedOrb, bool dequeue = true)
    {
        // Vanilla exits when CombatManager is over/ending. The simulator is used only from
        // live hover prediction and avoids consulting global combat-manager state.
        var orbQueue = State.GetPlayerCombatState(player).OrbQueue;
        if (orbQueue.Orbs.Count <= 0)
        {
            return;
        }

        if (dequeue)
        {
            _ = orbQueue.Remove(evokedOrb);
        }

        IReadOnlyList<Creature> targets;
        // Prediction-only instrumentation: damage mirrored during an orb evoke should be
        // attributed to that orb in shadow history without changing vanilla control flow.
        using (PushSource(evokedOrb))
        {
            targets = evokedOrb switch
            {
                LightningOrb lightningOrb => LightningOrbEvoke(lightningOrb),
                FrostOrb frostOrb => FrostOrbEvoke(frostOrb),
                DarkOrb darkOrb => DarkOrbEvoke(darkOrb),
                GlassOrb glassOrb => GlassOrbEvoke(glassOrb),
                PlasmaOrb plasmaOrb => PlasmaOrbEvoke(plasmaOrb),
                _ => UnsupportedOrbEvoke(evokedOrb)
            };
        }

        // Vanilla calls evokedOrb.RemoveInternal after AfterOrbEvoked when dequeue succeeds.
        // We only remove from the shadow queue because mutating the real orb would affect
        // gameplay state and save data.
        AfterOrbEvoked(evokedOrb, targets);
    }

    // Mirrors OrbCmd.Passive without VFX/SFX, choice-context model stack updates, or real orb mutation.
    public IReadOnlyList<Creature> OrbPassive(OrbModel orb, Creature? target = null)
    {
        using (PushSource(orb))
        {
            return orb switch
            {
                LightningOrb lightningOrb => LightningOrbPassive(lightningOrb, target),
                FrostOrb frostOrb => FrostOrbPassive(frostOrb),
                DarkOrb darkOrb => DarkOrbPassive(darkOrb),
                GlassOrb glassOrb => GlassOrbPassive(glassOrb),
                PlasmaOrb plasmaOrb => PlasmaOrbPassive(plasmaOrb),
                _ => UnsupportedOrbPassive(orb)
            };
        }
    }

    // Mirrors LightningOrb.Passive -> LightningOrb.ApplyLightningDamage(PassiveVal, target, choiceContext).
    private IReadOnlyList<Creature> LightningOrbPassive(LightningOrb orb, Creature? target)
    {
        return LightningOrbDamage(orb, orb.PassiveVal, target);
    }

    // Mirrors LightningOrb.Evoke -> LightningOrb.ApplyLightningDamage(EvokeVal, null, choiceContext).
    private IReadOnlyList<Creature> LightningOrbEvoke(LightningOrb orb)
    {
        return LightningOrbDamage(orb, orb.EvokeVal, target: null);
    }

    // Mirrors LightningOrb.ApplyLightningDamage without VFX/SFX.
    private IReadOnlyList<Creature> LightningOrbDamage(LightningOrb orb, decimal value, Creature? target)
    {
        var candidates = State.GetHittableOpponentsOf(orb.Owner.Creature);
        if (candidates.Count == 0)
        {
            return [];
        }

        target ??= Rng.CombatTargets.NextItem(candidates);
        if (target == null)
        {
            return [];
        }

        var targets = (IReadOnlyList<Creature>)[target];
        Damage(targets, value, ValueProp.Unpowered, orb.Owner.Creature);
        return targets;
    }

    // Mirrors FrostOrb.Passive -> CreatureCmd.GainBlock(owner, PassiveVal, Unpowered, null).
    private IReadOnlyList<Creature> FrostOrbPassive(FrostOrb orb)
    {
        GainBlock(orb.Owner.Creature, orb.PassiveVal, ValueProp.Unpowered);
        return [orb.Owner.Creature];
    }

    // Mirrors FrostOrb.Evoke -> CreatureCmd.GainBlock(owner, EvokeVal, Unpowered, null).
    private IReadOnlyList<Creature> FrostOrbEvoke(FrostOrb orb)
    {
        GainBlock(orb.Owner.Creature, orb.EvokeVal, ValueProp.Unpowered);
        return [orb.Owner.Creature];
    }

    // Mirrors DarkOrb.Passive by increasing the cloned orb's stored evoke value.
    private IReadOnlyList<Creature> DarkOrbPassive(DarkOrb orb)
    {
        orb._evokeVal += orb.PassiveVal;
        return [];
    }

    // Mirrors DarkOrb.Evoke -> damage the hittable enemy with the least current HP.
    private IReadOnlyList<Creature> DarkOrbEvoke(DarkOrb orb)
    {
        var target = State.GetHittableOpponentsOf(orb.Owner.Creature)
            .MinBy(creature => State.GetCreature(creature).CurrentHp);
        if (target == null)
        {
            return [];
        }

        var targets = (IReadOnlyList<Creature>)[target];
        Damage(targets, orb.EvokeVal, ValueProp.Unpowered, orb.Owner.Creature);
        return targets;
    }

    // Mirrors GlassOrb.Passive -> damage all hittable enemies, then reduce cloned base value.
    private IReadOnlyList<Creature> GlassOrbPassive(GlassOrb orb)
    {
        var targets = State.GetHittableOpponentsOf(orb.Owner.Creature);
        var passiveVal = orb.PassiveVal;
        if (passiveVal <= 0m)
        {
            return [];
        }

        // We can modify the orb's passive value because the simulator uses a cloned orb queue.
        // The real orb queue is not mutated.
        orb._passiveVal = Math.Max(0m, orb._passiveVal - 1m);
        Damage(targets, passiveVal, ValueProp.Unpowered, orb.Owner.Creature);
        return targets;
    }

    // Mirrors GlassOrb.Evoke -> damage all hittable enemies.
    private IReadOnlyList<Creature> GlassOrbEvoke(GlassOrb orb)
    {
        var targets = State.GetHittableOpponentsOf(orb.Owner.Creature);
        if (orb.EvokeVal <= 0m)
        {
            return [];
        }

        Damage(targets, orb.EvokeVal, ValueProp.Unpowered, orb.Owner.Creature);
        return targets;
    }

    // Mirrors PlasmaOrb.Passive.
    private IReadOnlyList<Creature> PlasmaOrbPassive(PlasmaOrb orb)
    {
        // Energy gain is not modeled in the combat prediction simulator.
        return [orb.Owner.Creature];
    }

    // Mirrors PlasmaOrb.Evoke.
    private IReadOnlyList<Creature> PlasmaOrbEvoke(PlasmaOrb orb)
    {
        // Energy gain is not modeled in the combat prediction simulator.
        return [orb.Owner.Creature];
    }

    private IReadOnlyList<Creature> UnsupportedOrbEvoke(OrbModel orb)
    {
        MarkCurrentSourceRisky();
        return [];
    }

    private IReadOnlyList<Creature> UnsupportedOrbPassive(OrbModel orb)
    {
        MarkCurrentSourceRisky();
        return [];
    }

    // Mirrors Hook.AfterOrbChanneled as a risk-only hook scan.
    private void AfterOrbChanneled(Player player, OrbModel orb)
    {
        OrbHooks.RunAfterOrbChanneled(new AfterOrbChanneledHookContext
        {
            Simulator = this,
            Player = player,
            Orb = orb
        });
    }

    // Mirrors Hook.AfterOrbEvoked as a risk-only hook scan in Phase 3.
    private void AfterOrbEvoked(OrbModel orb, IReadOnlyList<Creature> targets)
    {
        // Vanilla awaits Hook.AfterOrbEvoked. The preview path does not run async side effects;
        // OrbHooks only mirrors known handlers or records unsupported hook listeners as risk.
        OrbHooks.RunAfterOrbEvoked(new AfterOrbEvokedHookContext
        {
            Simulator = this,
            Orb = orb,
            Targets = targets
        });
    }
}
