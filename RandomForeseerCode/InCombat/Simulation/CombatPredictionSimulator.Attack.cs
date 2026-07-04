using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.InCombat.Hooks;

namespace RandomForeseer.RandomForeseerCode.InCombat.Simulation;

internal sealed partial class CombatPredictionSimulator
{
    // Mirrors the gameplay-relevant parts of AttackCommand.Execute without mutating real
    // creature/card state or running command-local arbitrary callbacks. Callers are
    // responsible for pushing the attack's card/monster source before calling this method;
    // Execute only pushes hook listeners through AttackHooks.
    public IReadOnlyList<IReadOnlyList<DamageResult>> ExecuteAttack(AttackCommand attackCommand)
    {
        if (attackCommand.Attacker is not { } attacker)
        {
            Entry.Logger.Warn("AttackCommand prediction skipped: command has no attacker.");
            MarkCurrentSourceRisky();
            return [];
        }

        var attackerState = State.GetCreature(attacker);
        if (attackerState.IsDead)
        {
            return [];
        }

        if (!attackCommand.IsSingleTargeted && !attackCommand.IsMultiTargeted)
        {
            Entry.Logger.Warn("AttackCommand prediction skipped: command has no targets configured.");
            MarkCurrentSourceRisky();
            return [];
        }

        AttackHooks.RunBefore(new BeforeAttackHookContext
        {
            Simulator = this,
            Command = attackCommand
        });

        var hitCount = AttackHooks.ModifyHitCount(new ModifyAttackHitCountHookContext
        {
            Simulator = this,
            Command = attackCommand,
            HitCount = attackCommand._hitCount
        });

        var hitResults = new List<IReadOnlyList<DamageResult>>();
        var cardSource = attackCommand.ModelSource is CardModel card
            ? State.FindCard(card) ?? new PredictedCard(card)
            : null;

        for (var i = 0; i < hitCount; i++)
        {
            if (attackerState.IsDead)
            {
                break;
            }

            var validTargets = GetPossibleAttackTargets(attackCommand)
                .Where(creature => State.GetCreature(creature).IsAlive)
                .ToList();
            if (validTargets.Count == 0)
            {
                break;
            }

            var singleTarget = SelectSingleAttackTarget(attackCommand, validTargets, hitResults);
            if (attackCommand.IsRandomlyTargeted && singleTarget == null)
            {
                break;
            }

            var results = Damage(
                singleTarget != null ? [singleTarget] : validTargets,
                GetAttackDamageAmount(attackCommand, singleTarget),
                attackCommand.DamageProps,
                attacker,
                cardSource);
            hitResults.Add(results);
        }

        RecordAttackHistory(
            attacker,
            attackCommand.ModelSource,
            hitResults.SelectMany(results => results).ToArray());

        AttackHooks.RunAfter(new AfterAttackHookContext
        {
            Simulator = this,
            Command = attackCommand,
            HitResults = hitResults
        });

        return hitResults;
    }

    // Mirrors AttackCommand.GetPossibleTargets but uses the simulator's state instead of the real CombatState.
    // Precondition: Execute has already verified that the command has an attacker and a target mode.
    private IReadOnlyList<Creature> GetPossibleAttackTargets(AttackCommand attackCommand)
    {
        if (attackCommand.Attacker is not { } attacker)
        {
            throw new InvalidOperationException("AttackCommand must have an attacker.");
        }

        if (attackCommand.IsSingleTargeted)
        {
            return [attackCommand._singleTarget!];
        }

        if (attackCommand.IsMultiTargeted)
        {
            return attackCommand._sourceType switch
            {
                AttackCommand.SourceType.Monster => State.PlayerCreatures,
                _ => State.GetOpponentsOf(attacker)
            };
        }

        throw new InvalidOperationException("AttackCommand must be either single-targeted or multi-targeted.");
    }

    private Creature? SelectSingleAttackTarget(
        AttackCommand attackCommand,
        List<Creature> validTargets,
        IReadOnlyList<IReadOnlyList<DamageResult>> previousHitResults)
    {
        if (!attackCommand.IsRandomlyTargeted)
        {
            return validTargets.Count == 1 ? validTargets[0] : null;
        }

        if (!attackCommand._doesRandomTargetingAllowDuplicates)
        {
            var previousReceivers = previousHitResults
                .SelectMany(results => results)
                .Select(result => result.Receiver)
                .ToHashSet();
            validTargets = validTargets
                .Where(creature => !previousReceivers.Contains(creature))
                .ToList();

            if (validTargets.Count == 0)
            {
                Entry.Logger.Warn("No valid targets available for randomly-targeted attack.");
                MarkCurrentSourceRisky();
                return null;
            }
        }

        return Rng.CombatTargets.NextItem(validTargets);
    }

    private decimal GetAttackDamageAmount(AttackCommand attackCommand, Creature? singleTarget)
    {
        if (attackCommand._calculatedDamageVar is {} calculatedDamageVar)
        {
            // TODO: This might need more careful review to ensure that all modifiers and context are
            // correctly applied in the simulation.
            MarkCurrentSourceRisky();
            return calculatedDamageVar.Calculate(singleTarget);
        }

        return attackCommand._damagePerHit;
    }
}
