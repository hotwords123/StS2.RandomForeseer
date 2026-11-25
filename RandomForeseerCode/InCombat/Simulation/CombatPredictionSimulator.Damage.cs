using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using RandomForeseer.Common;
using RandomForeseer.InCombat.Mirrors;

namespace RandomForeseer.InCombat.Simulation;

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
        CardPlay? cardPlay,
        ModifyDamageHookType modifyDamageHookType = ModifyDamageHookType.All)
    {
        if (dealer is not null && State.GetCreature(dealer).IsDead || targets.Count == 0)
        {
            return [.. targets.Select(target => new DamageResult(target, props))];
        }

        List<DamageResult> damageResults = [];
        foreach (var target in targets)
        {
            var targetState = State.GetCreature(target);
            if (targetState.IsDead)
            {
                continue;
            }

            var modifiedAmount = HookMirrors.ModifyDamage(
                this,
                target,
                dealer,
                amount,
                props,
                cardSource,
                cardPlay,
                modifyDamageHookType);
            // Hook.AfterModifyingDamageAmount is skipped here because all vanilla listeners are visual-only.

            var targetResults = GetDamageResults(targetState, modifiedAmount, props, dealer, cardSource);
            HookMirrors.BeforeDamageReceived(this, target, modifiedAmount, props, dealer, cardSource);
            ApplyDamageResults(targetResults);

            foreach (var result in targetResults.AllResults())
            {
                // Vanilla checks IsInProgress && !IsEnding before writing combat history.
                // The damage preview reads prediction history, so record the hit even when it ends combat.
                History.DamageReceived(result.Receiver, dealer, result, cardSource);
            }

            damageResults.AddRange(targetResults.AllResults());
        }

        ProcessDamageResults(damageResults, dealer, cardSource);
        return damageResults;
    }

    /// <summary>
    /// Mirrors the dry-run calculation in <see cref="CreatureCmd.GetDamageResults"/>.
    /// </summary>
    /// <remarks>
    /// Currently, the vanilla implementation of this method leaves the <c>cardPlay</c> parameter unused, so it is
    /// omitted here. If that changes in the future, this method should be updated.
    /// </remarks>
    private DamageResults GetDamageResults(
        SimCreatureState targetState,
        decimal damage,
        ValueProp props,
        Creature? dealer,
        PredictedCard? cardSource)
    {
        var originalTarget = targetState.Creature;

        var preBlockTarget = HookMirrors.ModifyPreBlockDamageTarget(this, originalTarget, damage, props, dealer);
        DamageResult? preBlockResult = null;
        List<AbstractModel> beforeWuppyModifiers = [];
        if (preBlockTarget != null)
        {
            var preBlockDamage = HookMirrors.ModifyHpLost(
                this,
                originalTarget,
                damage,
                props,
                dealer,
                cardSource,
                HpLossHookPhase.BeforeWuppy,
                out beforeWuppyModifiers);
            preBlockResult = PreviewHpDamage(State.GetCreature(preBlockTarget), preBlockDamage, props);
            preBlockResult.IntendedHpLoss = (int)damage;
            preBlockResult.OriginalTarget = originalTarget;
            damage = preBlockResult.OverkillDamage;
        }

        var blockTarget = originalTarget.PetOwner?.Creature ?? originalTarget;
        var blockTargetState = State.GetCreature(blockTarget);
        var blockedDamage = blockTargetState.DamageBlock(damage, props, dryRun: true);
        damage = Math.Max(damage - blockedDamage, 0m);

        var unblockedDamage = HookMirrors.ModifyHpLost(
            this,
            originalTarget,
            damage,
            props,
            dealer,
            cardSource,
            HpLossHookPhase.BeforeOsty,
            out var beforeOstyModifiers);

        var unblockedDamageTarget = HookMirrors.ModifyUnblockedDamageTarget(
            this,
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

        var unblockedResult = PreviewHpDamage(State.GetCreature(unblockedDamageTarget), unblockedDamage, props);
        unblockedResult.OriginalTarget = originalTarget;
        unblockedResult.IntendedHpLoss = (int)damage;

        var wasBlockBroken = targetState.Block <= blockedDamage && blockedDamage > 0m;
        var wasFullyBlocked = !props.HasFlag(ValueProp.Unblockable) &&
                              (blockedDamage > 0m || targetState.Block > 0) &&
                              (int)unblockedDamage == 0;

        DamageResult originalResult;
        DamageResult? blockRedirect = null;
        DamageResult? unblockedRedirect = null;
        List<AbstractModel> overflowModifiers = [];
        if (originalTarget == unblockedDamageTarget)
        {
            originalResult = unblockedResult;
            if (originalTarget != blockTarget)
            {
                blockRedirect = new DamageResult(blockTarget, props)
                {
                    IntendedHpLoss = unblockedResult.OverkillDamage,
                    BlockedDamage = (int)blockedDamage,
                    WasBlockBroken = wasBlockBroken,
                    WasFullyBlocked = wasFullyBlocked
                };
            }
        }
        else
        {
            unblockedRedirect = unblockedResult;
            var originalTargetDamage = HookMirrors.ModifyHpLost(
                this,
                originalTarget,
                unblockedResult.OverkillDamage,
                props,
                dealer,
                cardSource,
                HpLossHookPhase.AfterOsty,
                out overflowModifiers);

            originalResult = originalTargetDamage > 0m
                ? PreviewHpDamage(targetState, originalTargetDamage, props)
                : new DamageResult(originalTarget, props);
            originalResult.IntendedHpLoss = unblockedResult.OverkillDamage;
        }

        originalResult.BlockedDamage = (int)blockedDamage;
        originalResult.WasBlockBroken = wasBlockBroken;
        originalResult.WasFullyBlocked = wasFullyBlocked;

        return new DamageResults
        {
            original = originalResult,
            preBlockRedirect = preBlockResult,
            blockRedirect = blockRedirect,
            unblockedRedirect = unblockedRedirect,
            beforeWuppyHpLossModifiers = beforeWuppyModifiers,
            beforeOstyHpLossModifiers = beforeOstyModifiers,
            afterOstyHpLossModifiers = afterOstyModifiers,
            afterOstyHpLossModifiers2 = overflowModifiers
        };
    }

    private static DamageResult PreviewHpDamage(SimCreatureState target, decimal amount, ValueProp props)
    {
        var loss = target.LoseHp(amount, dryRun: true);
        return new DamageResult(target.Creature, props)
        {
            UnblockedDamage = loss.hpLost,
            WasTargetKilled = loss.killed,
            OverkillDamage = loss.overkillAmount
        };
    }

    /// <summary>
    /// Mirrors <see cref="CreatureCmd.ApplyDamageResults"/>.
    /// </summary>
    private void ApplyDamageResults(DamageResults results)
    {
        if (results.preBlockRedirect is { } preBlockRedirect)
        {
            State.GetCreature(preBlockRedirect.Receiver).LoseHp(preBlockRedirect.UnblockedDamage);
        }

        if (results.blockRedirect is { } blockRedirect)
        {
            State.GetCreature(blockRedirect.Receiver).DamageBlock(blockRedirect.BlockedDamage, blockRedirect.Props);
        }

        var originalState = State.GetCreature(results.original.Receiver);
        originalState.DamageBlock(results.original.BlockedDamage, results.original.Props);

        // The BeforeOsty callbacks are visual-only in vanilla, so they are not mirrored here.

        if (results.unblockedRedirect is { } unblockedRedirect)
        {
            State.GetCreature(unblockedRedirect.Receiver).LoseHp(unblockedRedirect.UnblockedDamage);
        }

        if (results.afterOstyHpLossModifiers.Count > 0)
        {
            HookMirrors.AfterModifyingHpLostAfterOsty(this, results.afterOstyHpLossModifiers);
        }

        originalState.LoseHp(results.original.UnblockedDamage);
        if (results.afterOstyHpLossModifiers2.Count > 0)
        {
            HookMirrors.AfterModifyingHpLostAfterOsty(this, results.afterOstyHpLossModifiers2);
        }
    }

    // Mirrors the post-target DamageResult processing in CreatureCmd.Damage.
    private void ProcessDamageResults(IEnumerable<DamageResult> results, Creature? dealer, PredictedCard? cardSource)
    {
        var killedCreatures = new List<Creature>();
        foreach (var damageResult in results)
        {
            var receiver = damageResult.Receiver;

            if (damageResult.WasBlockBroken)
            {
                HookMirrors.AfterBlockBroken(this, receiver, dealer);
            }

            if (damageResult.UnblockedDamage > 0)
            {
                HookMirrors.AfterCurrentHpChanged(this, receiver, -damageResult.UnblockedDamage);
            }

            HookMirrors.AfterDamageGiven(this, receiver, damageResult, damageResult.Props, dealer, cardSource);

            if (!damageResult.WasTargetKilled || !State.GetCreature(receiver).IsDead)
            {
                HookMirrors.AfterDamageReceived(
                    this,
                    receiver,
                    damageResult,
                    damageResult.Props,
                    dealer,
                    cardSource);
            }
            else
            {
                killedCreatures.Add(receiver);
            }
        }

        Kill(killedCreatures, cardSource: cardSource);
    }

    // Convenience overload for Kill with a single target.
    public void Kill(Creature creature, bool force = false, PredictedCard? cardSource = null)
    {
        Kill([creature], force, cardSource);
    }

    // Mirrors CreatureCmd.Kill.
    public void Kill(IReadOnlyList<Creature> creatures, bool force = false, PredictedCard? cardSource = null)
    {
        foreach (var creature in creatures)
        {
            KillWithoutCheckingWinCondition(creature, force, cardSource);
        }

        if (State.Players.All(player => State.GetCreature(player.Creature).IsDead))
        {
            LoseCombat();
        }

        // Vanilla ends a player's turn when the player is killed, which is not simulated here.
    }

    // Mirrors CreatureCmd.KillWithoutCheckingWinCondition, without recursion checks.
    private void KillWithoutCheckingWinCondition(Creature creature, bool force, PredictedCard? cardSource)
    {
        var creatureState = State.GetCreature(creature);
        var currentHp = creatureState.CurrentHp;
        if (currentHp > 0)
        {
            creatureState.LoseHp(currentHp);
            HookMirrors.AfterCurrentHpChanged(this, creature, -currentHp);
        }

        HookMirrors.BeforeDeath(this, creature);

        if (force || creature.MaxHp <= 0 || HookMirrors.ShouldDie(this, creature, out var preventer))
        {
            var shouldRemoveFromCombat = HookMirrors.ShouldCreatureBeRemovedFromCombatAfterDeath(this, creature);

            HookMirrors.AfterDeath(this, creature, wasRemovalPrevented: false, source: cardSource);

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
                    Kill(aliveTeammates, cardSource: cardSource);
                }
            }
            else if (creature.Player is { } player)
            {
                HandlePlayerDeath(player);
            }
        }
        else
        {
            HookMirrors.AfterDeath(this, creature, wasRemovalPrevented: true, source: cardSource);
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
