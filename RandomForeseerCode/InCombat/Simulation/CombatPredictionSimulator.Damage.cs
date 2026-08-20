using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.InCombat.Mirrors;

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

        var modifiedAmount = HookMirrors.ModifyDamage(
            this,
            originalTarget,
            dealer, amount,
            props,
            cardSource,
            cardPlay);

        HookMirrors.BeforeDamageReceived(
            this,
            originalTarget,
            modifiedAmount,
            props,
            dealer,
            cardSource);

        var blockTarget = originalTarget.PetOwner?.Creature ?? originalTarget;
        var blockTargetState = State.GetCreature(blockTarget);
        var blockedDamage = blockTargetState.DamageBlock(modifiedAmount, props);

        var unblockedDamage = HookMirrors.ModifyHpLost(
            this,
            originalTarget,
            Math.Max(modifiedAmount - blockedDamage, 0m),
            props,
            dealer,
            cardSource,
            HpLossHookPhase.BeforeOsty,
            out _);

        var unblockedDamageTarget = Hook.ModifyUnblockedDamageTarget(
            State.CombatState,
            originalTarget,
            unblockedDamage,
            props,
            dealer);

        unblockedDamage = HookMirrors.ModifyHpLost(
            this,
            unblockedDamageTarget,
            unblockedDamage,
            props,
            dealer,
            cardSource,
            HpLossHookPhase.AfterOsty,
            out var afterOstyModifiers);
        HookMirrors.AfterModifyingHpLostAfterOsty(this, afterOstyModifiers);

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
            var originalTargetDamage = HookMirrors.ModifyHpLost(
                this,
                originalTarget,
                unblockedDamageResult.OverkillDamage,
                props,
                dealer,
                cardSource,
                HpLossHookPhase.AfterOsty,
                out var redirectedAfterOstyModifiers);
            HookMirrors.AfterModifyingHpLostAfterOsty(this, redirectedAfterOstyModifiers);

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
            History.DamageReceived(
                damageResult.Receiver,
                dealer,
                damageResult,
                cardSource);
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
                HookMirrors.AfterBlockBroken(this, originalTarget, dealer);
            }

            if (damageResult.UnblockedDamage > 0)
            {
                HookMirrors.AfterCurrentHpChanged(this, originalTarget, -damageResult.UnblockedDamage);
            }

            HookMirrors.AfterDamageGiven(
                this,
                originalTarget,
                damageResult,
                damageResult.Props,
                dealer,
                cardSource);

            if (!damageResult.WasTargetKilled || !State.GetCreature(originalTarget).IsDead)
            {
                HookMirrors.AfterDamageReceived(
                    this,
                    originalTarget,
                    damageResult,
                    damageResult.Props,
                    dealer,
                    cardSource);
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

        if (State.Players.All(player => State.GetCreature(player.Creature).IsDead))
        {
            LoseCombat();
        }

        // Vanilla ends a player's turn when the player is killed, which is not simulated here.
    }

    // Mirrors CreatureCmd.KillWithoutCheckingWinCondition, without recursion checks.
    private void KillWithoutCheckingWinCondition(Creature creature, bool force)
    {
        var creatureState = State.GetCreature(creature);
        var currentHp = creatureState.CurrentHp;
        if (currentHp > 0)
        {
            creatureState.LoseHp(currentHp, ValueProp.Unblockable | ValueProp.Unpowered);
            HookMirrors.AfterCurrentHpChanged(this, creature, -currentHp);
        }

        HookMirrors.BeforeDeath(this, creature);

        if (force || creature.MaxHp <= 0 || HookMirrors.ShouldDie(this, creature, out var preventer))
        {
            var shouldRemoveFromCombat = Hook.ShouldCreatureBeRemovedFromCombatAfterDeath(State.CombatState, creature);

            HookMirrors.AfterDeath(this, creature, wasRemovalPrevented: false);

            var aliveTeammates = State.GetTeammatesOf(creature)
                .Where(c => State.GetCreature(c).IsAlive)
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
                if (isPrimaryEnemy && aliveTeammates.Length > 0 && aliveTeammates.All(c => c.IsSecondaryEnemy))
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
            HookMirrors.AfterDeath(this, creature, wasRemovalPrevented: true);
            HookMirrors.AfterPreventingDeath(this, preventer, creature);

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
        if (!State.Players.All(p => State.GetCreature(p.Creature).IsDead))
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
