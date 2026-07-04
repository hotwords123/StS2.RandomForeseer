using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.InCombat.Hooks;

namespace RandomForeseer.RandomForeseerCode.InCombat.Simulation;

internal sealed partial class CombatPredictionSimulator
{
    // Convenience overload for Damage with a single target and a DamageVar.
    public IReadOnlyList<DamageResult> Damage(
        Creature target,
        DamageVar damageVar,
        Creature? dealer,
        PredictedCard? cardSource = null)
    {
        return Damage([target], damageVar.BaseValue, damageVar.Props, dealer, cardSource);
    }

    // Convenience overload for Damage with a single target.
    public IReadOnlyList<DamageResult> Damage(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        PredictedCard? cardSource = null)
    {
        return Damage([target], amount, props, dealer, cardSource);
    }

    // Convenience overload for Damage with a DamageVar.
    public IReadOnlyList<DamageResult> Damage(
        IReadOnlyList<Creature> targets,
        DamageVar damageVar,
        Creature? dealer,
        PredictedCard? cardSource = null)
    {
        return Damage(targets, damageVar.BaseValue, damageVar.Props, dealer, cardSource);
    }

    // Mirrors CreatureCmd.Damage without mutating real Creature state.
    public IReadOnlyList<DamageResult> Damage(
        IReadOnlyList<Creature> targets,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        PredictedCard? cardSource = null)
    {
        if (dealer?.IsDead == true || targets.Count == 0)
        {
            // Vanilla returns empty DamageResult shells when the dealer is dead. The simulator
            // only uses damage results to update prediction state, so no-op results are omitted.
            return [];
        }

        var results = new List<DamageResult>();
        var runState = IRunState.GetFrom(targets.Append(dealer).OfType<Creature>());

        foreach (var originalTarget in targets)
        {
            results.AddRange(DamageTarget(originalTarget, amount, props, dealer, cardSource, runState));
        }

        ProcessDamageResults(results, runState, dealer, cardSource);
        return results;
    }

    // Mirrors the per-target body of CreatureCmd.Damage.
    private IReadOnlyList<DamageResult> DamageTarget(
        Creature originalTarget,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        PredictedCard? cardSource,
        IRunState runState)
    {
        var originalTargetState = State.GetCreature(originalTarget);
        if (originalTargetState.IsDead)
        {
            return [];
        }

        var modifiedAmount = Hook.ModifyDamage(
            runState,
            State.CombatState,
            originalTarget,
            dealer,
            amount,
            props,
            cardSource?.Preview,
            // StS2 v0.108.0 passes CardPlay into damage modifiers during real card execution.
            // Forecast damage uses the same no-CardPlay shape as vanilla preview calculations.
            null,
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
        var damageResults = new List<DamageResult> { unblockedDamageResult };

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

        return damageResults;
    }

    // Mirrors the post-target DamageResult processing in CreatureCmd.Damage.
    private void ProcessDamageResults(
        IEnumerable<DamageResult> results,
        IRunState runState,
        Creature? dealer,
        PredictedCard? cardSource)
    {
        var killedCreatures = new List<Creature>();
        foreach (var damageResult in results)
        {
            var originalTarget = damageResult.Receiver;

            // Mirrors CombatManager.Instance.History.DamageReceived in CreatureCmd.Damage,
            // but writes to simulator shadow history instead of the live combat history.
            RecordDamageHistory(damageResult, dealer, cardSource);

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

        foreach (var creature in killedCreatures)
        {
            ProcessDeath(creature, runState);
        }
    }

    // Mirrors the death-processing portion of CreatureCmd.KillWithoutCheckingWinCondition.
    // Phase 2 only updates shadow liveness and records unsupported death hook side effects.
    // Vanilla also forces remaining HP to 0, invokes Died events, removes creatures from
    // combat, removes powers, kills secondary enemies/Osty, clears player orbs, and handles
    // player death. Those broad combat-structure mutations are not mirrored until the
    // simulator owns shadow creature lists, powers, and player combat state.
    private void ProcessDeath(Creature creature, IRunState runState)
    {
        // BeforeDeath and AfterDeath are async gameplay hooks. The simulator mirrors targeted
        // prediction-relevant side effects and records unsupported overrides as drift risk.
        DeathHooks.RunBeforeDeath(new BeforeDeathHookContext
        {
            Simulator = this,
            Creature = creature
        });

        if (!Hook.ShouldDie(runState, State.CombatState, creature, out var preventer))
        {
            if (preventer != null)
            {
                using (PushSource(preventer))
                {
                    MarkCurrentSourceRisky();
                }
            }

            State.GetCreature(creature).PreventDeath();
            // Vanilla calls Hook.AfterPreventingDeath here. The simulator intentionally
            // omits that mirror until prevented-death side effects are modeled.
            DeathHooks.RunAfterDeath(new AfterDeathHookContext
            {
                Simulator = this,
                Creature = creature,
                WasRemovalPrevented = true
            });
            return;
        }

        // Vanilla checks Hook.ShouldCreatureBeRemovedFromCombatAfterDeath before removing
        // creatures from combat. The simulator does not model creature removal yet, so this
        // mirror is intentionally omitted.
        DeathHooks.RunAfterDeath(new AfterDeathHookContext
        {
            Simulator = this,
            Creature = creature,
            WasRemovalPrevented = false
        });
    }
}
