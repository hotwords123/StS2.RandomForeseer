using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using RandomForeseer.RandomForeseerCode.InCombat.Hooks;
using RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Orbs;

namespace RandomForeseer.RandomForeseerCode.InCombat.Simulation;

internal sealed partial class CombatPredictionSimulator
{
    // Mirrors OrbQueue.BeforeTurnEnd without waits, real queue mutation, or async hook execution.
    private void SimulateOrbQueueBeforeTurnEnd(Player player)
    {
        var orbQueue = State.GetPlayerCombatState(player).OrbQueue;
        foreach (var orb in orbQueue.Orbs.ToList())
        {
            OrbMirrors.InvokeBeforeTurnEndOrbTrigger(this, orb);
        }
    }

    // Mirrors OrbModel.TriggerPassive without VFX/SFX, waits, or real model-stack updates.
    internal void TriggerOrbPassive(OrbModel orb, Creature? target)
    {
        var passiveCountContext = new OrbPassiveCountHookContext
        {
            Simulator = this,
            Orb = orb
        };
        var triggerCount = OrbPassiveCountHooks.ModifyOrbPassiveTriggerCount(passiveCountContext);

        for (var i = 0; i < triggerCount; i++)
        {
            OrbMirrors.InvokePassive(this, orb, target);
        }
    }

    // Mirrors OrbCmd.Channel<T> without mutating the real orb queue.
    public void OrbChannel<T>(Player player, int count = 1) where T : OrbModel
    {
        for (var i = 0; i < count; i++)
        {
            OrbChannel(player, ModelDb.Orb<T>().ToMutable());
        }
    }

    // Mirrors OrbCmd.Channel without VFX/SFX, waits, real queue mutation, or async hook execution.
    public bool OrbChannel(Player player, OrbModel orb)
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
            return false;
        }

        RecordOrbChanneledHistory(orb);
        AfterOrbChanneled(player, orb);
        return true;
    }

    // Mirrors OrbCmd.EvokeNext without mutating the real orb queue.
    public void OrbEvokeNext(Player player, int repeat = 1, bool dequeue = true)
    {
        var orbQueue = State.GetPlayerCombatState(player).OrbQueue;
        if (orbQueue.Orbs.Count > 0)
        {
            var orb = orbQueue.Orbs[0];
            for (int i = 0; i < repeat; i++)
            {
                OrbEvoke(player, orb, dequeue: dequeue && i == repeat - 1);
            }
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

        var targets = OrbMirrors.InvokeEvoke(this, evokedOrb);

        // Vanilla calls evokedOrb.RemoveInternal after AfterOrbEvoked when dequeue succeeds.
        // We only remove from the shadow queue because mutating the real orb would affect
        // gameplay state and save data.
        AfterOrbEvoked(evokedOrb, targets);
    }

    // Mirrors OrbCmd.Passive without VFX/SFX, choice-context model stack updates, or real orb mutation.
    public void OrbPassive(OrbModel orb, Creature? target = null)
    {
        OrbMirrors.InvokePassive(this, orb, target);
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
