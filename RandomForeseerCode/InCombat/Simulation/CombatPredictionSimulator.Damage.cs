using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.InCombat.Hooks;

namespace RandomForeseer.RandomForeseerCode.InCombat.Simulation;

internal sealed partial class CombatPredictionSimulator
{
    // Convenience overload for non-card Damage with a single target and a DamageVar.
    public IReadOnlyList<DamageResult> Damage(
        Creature target,
        DamageVar damageVar,
        Creature? dealer)
    {
        return Damage([target], damageVar.BaseValue, damageVar.Props, dealer, cardSource: null, cardPlay: null);
    }

    // Convenience overload for non-card Damage with a single target.
    public IReadOnlyList<DamageResult> Damage(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer)
    {
        return Damage([target], amount, props, dealer, cardSource: null, cardPlay: null);
    }

    // Convenience overload for non-card Damage with a DamageVar.
    public IReadOnlyList<DamageResult> Damage(
        IReadOnlyList<Creature> targets,
        DamageVar damageVar,
        Creature? dealer)
    {
        return Damage(targets, damageVar.BaseValue, damageVar.Props, dealer, cardSource: null, cardPlay: null);
    }

    // Convenience overload for non-card Damage.
    public IReadOnlyList<DamageResult> Damage(
        IReadOnlyList<Creature> targets,
        decimal amount,
        ValueProp props,
        Creature? dealer)
    {
        return Damage(targets, amount, props, dealer, cardSource: null, cardPlay: null);
    }

    // Mirrors CreatureCmd.Damage without mutating real Creature state.
    public IReadOnlyList<DamageResult> Damage(
        IReadOnlyList<Creature> targets,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        PredictedCard? cardSource,
        CardPlay? cardPlay)
    {
        if (dealer?.IsDead == true || targets.Count == 0)
        {
            // Vanilla returns empty DamageResult shells when the dealer is dead. The simulator
            // only uses damage results to update prediction state, so no-op results are omitted.
            return [];
        }

        var results = new List<DamageResult>();

        foreach (var originalTarget in targets)
        {
            results.AddRange(DamageTarget(originalTarget, amount, props, dealer, cardSource, cardPlay));
        }

        ProcessDamageResults(results, dealer, cardSource);
        return results;
    }

    // Mirrors the per-target body of CreatureCmd.Damage.
    private IReadOnlyList<DamageResult> DamageTarget(
        Creature originalTarget,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        PredictedCard? cardSource,
        CardPlay? cardPlay)
    {
        var originalTargetState = State.GetCreature(originalTarget);
        if (originalTargetState.IsDead)
        {
            return [];
        }

        var runState = combatState.RunState;
        var modifiedAmount = Hook.ModifyDamage(
            runState,
            State.CombatState,
            originalTarget,
            dealer,
            amount,
            props,
            cardSource?.Preview,
            // StS2 v0.108.0 passes CardPlay into damage modifiers during real card execution.
            cardPlay,
            ModifyDamageHookType.All,
            CardPreviewMode.None,
            out var damageModifiers);
        // Hook.Modify* is the same read-only value path used by vanilla previews. It is
        // safe to call directly; AfterModifying* usually only keeps modifier-internal
        // state in sync and is intentionally left for targeted hook mirrors.
        _ = damageModifiers;

        DamageReceivedHooks.RunBefore(new BeforeDamageReceivedHookContext
        {
            Simulator = this,
            Target = originalTarget,
            Props = props,
            Dealer = dealer,
            Source = cardSource
        });

        var blockTarget = originalTarget.PetOwner?.Creature ?? originalTarget;
        var blockTargetState = State.GetCreature(blockTarget);
        var blockedDamage = blockTargetState.DamageBlock(modifiedAmount, props);

        var unblockedDamage = Hook.ModifyHpLost(
            runState,
            State.CombatState,
            originalTarget,
            Math.Max(modifiedAmount - blockedDamage, 0m),
            props,
            dealer,
            cardSource?.Preview,
            HpLossHookPhase.BeforeOsty,
            out var beforeOstyModifiers);
        _ = beforeOstyModifiers;

        var unblockedDamageTarget = Hook.ModifyUnblockedDamageTarget(
            State.CombatState,
            originalTarget,
            unblockedDamage,
            props,
            dealer);

        unblockedDamage = Hook.ModifyHpLost(
            runState,
            State.CombatState,
            unblockedDamageTarget,
            unblockedDamage,
            props,
            dealer,
            cardSource?.Preview,
            HpLossHookPhase.AfterOsty,
            out var afterOstyModifiers);
        DamageModifierHooks.RunAfterModifyingHpLostAfterOsty(
            afterOstyModifiers,
            new AfterModifyingHpLostHookContext
            {
                Simulator = this
            });

        var unblockedDamageTargetState = State.GetCreature(unblockedDamageTarget);
        var unblockedDamageResult = unblockedDamageTargetState.LoseHp(unblockedDamage, props);
        List<DamageResult> damageResults = [unblockedDamageResult];

        var wasBlockBroken = originalTargetState.Block <= 0 && blockedDamage > 0m;
        var wasFullyBlocked = !props.HasFlag(ValueProp.Unblockable) &&
            (blockedDamage > 0m || originalTargetState.Block > 0) &&
            (int)unblockedDamage == 0;

        if (originalTarget == unblockedDamageTarget)
        {
            unblockedDamageResult.BlockedDamage = (int)blockedDamage;
            unblockedDamageResult.WasBlockBroken = wasBlockBroken;
            unblockedDamageResult.WasFullyBlocked = wasFullyBlocked;
        }
        else
        {
            var originalTargetDamage = Hook.ModifyHpLost(
                runState,
                State.CombatState,
                originalTarget,
                unblockedDamageResult.OverkillDamage,
                props,
                dealer,
                cardSource?.Preview,
                HpLossHookPhase.AfterOsty,
                out var redirectedAfterOstyModifiers);
            DamageModifierHooks.RunAfterModifyingHpLostAfterOsty(
                redirectedAfterOstyModifiers,
                new AfterModifyingHpLostHookContext
                {
                    Simulator = this
                });

            var damageResult = originalTargetDamage > 0m
                ? originalTargetState.LoseHp(originalTargetDamage, props)
                : new DamageResult(originalTarget, props);
            damageResult.BlockedDamage = (int)blockedDamage;
            damageResult.WasBlockBroken = wasBlockBroken;
            damageResult.WasFullyBlocked = wasFullyBlocked;
            damageResults.Add(damageResult);
        }

        foreach (var damageResult in damageResults)
        {
            // Mirrors CombatManager.Instance.History.DamageReceived in CreatureCmd.Damage,
            // but writes to simulator shadow history instead of the live combat history.
            RecordDamageHistory(damageResult, dealer, cardSource);
        }

        return damageResults;
    }

    // Mirrors the post-target DamageResult processing in CreatureCmd.Damage.
    private void ProcessDamageResults(IEnumerable<DamageResult> results, Creature? dealer, PredictedCard? cardSource)
    {
        var killedCreatures = new List<Creature>();
        foreach (var damageResult in results)
        {
            var originalTarget = damageResult.Receiver;

            if (damageResult.WasBlockBroken)
            {
                // Vanilla calls Hook.AfterBlockBroken here. Only BurrowedPower currently
                // overrides it, and that stun/power-removal flow does not affect current
                // prediction results, so it is intentionally not mirrored.
            }

            if (damageResult.UnblockedDamage > 0)
            {
                AfterCurrentHpChangedHook.Run(new AfterCurrentHpChangedHookContext
                {
                    Simulator = this,
                    Creature = originalTarget,
                    Delta = -damageResult.UnblockedDamage
                });
            }

            DamageGivenHooks.Run(new AfterDamageGivenHookContext
            {
                Simulator = this,
                Target = originalTarget,
                Result = damageResult,
                Props = damageResult.Props,
                Dealer = dealer,
                Source = cardSource
            });

            if (!damageResult.WasTargetKilled || !State.GetCreature(originalTarget).IsDead)
            {
                var context = new AfterDamageReceivedHookContext
                {
                    Simulator = this,
                    Target = originalTarget,
                    Result = damageResult,
                    Props = damageResult.Props,
                    Dealer = dealer,
                    Source = cardSource
                };
                DamageReceivedHooks.RunAfter(context);
                DamageReceivedHooks.RunAfterLate(context);
            }
            else
            {
                killedCreatures.Add(originalTarget);
            }
        }

        Kill(killedCreatures);
    }

    // Convenience overload for Kill with a single target.
    public void Kill(Creature creature, bool force = false)
    {
        Kill([creature], force);
    }

    // Mirrors CreatureCmd.Kill.
    public void Kill(IReadOnlyList<Creature> creatures, bool force = false)
    {
        foreach (var creature in creatures)
        {
            KillWithoutCheckingWinCondition(creature, force);
        }

        // Vanilla ends a player's turn when the player is killed, which is not simulated here.
    }

    // Mirrors CreatureCmd.KillWithoutCheckingWinCondition, without recursion checks.
    private void KillWithoutCheckingWinCondition(Creature creature, bool force)
    {
        var runState = combatState.RunState;

        var creatureState = State.GetCreature(creature);
        var currentHp = creatureState.CurrentHp;
        if (currentHp > 0)
        {
            creatureState.LoseHp(currentHp, ValueProp.Unblockable | ValueProp.Unpowered);
            AfterCurrentHpChangedHook.Run(new AfterCurrentHpChangedHookContext
            {
                Simulator = this,
                Creature = creature,
                Delta = -currentHp
            });
        }

        DeathHooks.RunBeforeDeath(new BeforeDeathHookContext
        {
            Simulator = this,
            Creature = creature
        });

        if (force || creature.MaxHp <= 0 || DeathPreventHooks.RunShouldDie(this, creature, out var preventer))
        {
            var shouldRemoveFromCombat = Hook.ShouldCreatureBeRemovedFromCombatAfterDeath(combatState, creature);

            DeathHooks.RunAfterDeath(new AfterDeathHookContext
            {
                Simulator = this,
                Creature = creature,
                WasRemovalPrevented = false
            });

            var aliveTeammates = State.GetTeammatesOf(creature)
                .Where(creature => State.GetCreature(creature).IsAlive)
                .ToArray();

            if (shouldRemoveFromCombat && creature.Side == CombatSide.Enemy && State.Enemies.Contains(creature))
            {
                // Vanilla also checks creature.Monster.IsPerformingMove here, which is omitted in the simulator
                // because we do not simulate monster moves.
                State.RemoveCreature(creature);
            }

            var isPrimaryEnemy = creature.IsPrimaryEnemy;

            // TODO: Vanilla removes all powers from the dead creature here.

            if (creature.Side == CombatSide.Enemy)
            {
                if (isPrimaryEnemy && aliveTeammates.Length > 0 && aliveTeammates.All(creature => creature.IsSecondaryEnemy))
                {
                    Kill(aliveTeammates);
                }
            }
            else if (creature.Player is { } player)
            {
                HandlePlayerDeath(player);
            }
        }
        else
        {
            DeathHooks.RunAfterDeath(new AfterDeathHookContext
            {
                Simulator = this,
                Creature = creature,
                WasRemovalPrevented = true
            });

            DeathPreventHooks.RunAfterPreventingDeath(this, preventer, creature);

            // Vanilla recursively calls KillWithoutCheckingWinCondition here, which is not mirrored.
        }
    }

    // Mirrors the player-death flow in CreatureCmd.KillWithoutCheckingWinCondition.
    private void HandlePlayerDeath(Player player)
    {
        var playerState = State.GetPlayerCombatState(player);
        playerState.OrbQueue.Clear();

        if (player.Osty is { } osty && State.GetCreature(osty).IsAlive)
        {
            Kill(osty, force: true);
        }

        // TODO: Vanilla sets player.IsActiveForHooks to false here.

        // Mirrors CombatManager.HandlePlayerDeath, which is only called when not all players are dead.
        if (!State.Players.All(player => State.GetCreature(player.Creature).IsDead))
        {
            RemoveFromCombat([.. playerState.AllCards]);

            // Vanilla calls PlayerCmd.Set{Energy,Stars} here, which in turn calls PlayerCmd.Lose{Energy,Stars}.
            // Technically, this can trigger some hooks, but since the player is dead, those hooks are not likely
            // to have any meaningful effect. Therefore, they are not mirrored here.
            playerState.LoseEnergy(playerState.Energy);
            playerState.LoseStars(playerState.Stars);
        }
    }
}
