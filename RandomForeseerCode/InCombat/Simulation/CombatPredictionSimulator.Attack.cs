using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.InCombat.Mirrors;

namespace RandomForeseer.RandomForeseerCode.InCombat.Simulation;

internal sealed partial class CombatPredictionSimulator
{
    // Mirrors the gameplay-relevant parts of AttackCommand.Execute without mutating real
    // creature/card state or running command-local arbitrary callbacks. Callers are
    // responsible for pushing the attack's card/monster source before calling this method;
    // Execute only pushes hook listeners through HookMirrors.
    public void ExecuteAttack(AttackCommand attackCommand)
    {
        if (attackCommand.Attacker is not { } attacker)
        {
            Entry.Logger.Warn("AttackCommand prediction skipped: command has no attacker.");
            History.RecordRisk(PredictionRiskReason.MethodMirrorIncomplete);
            return;
        }

        var attackerState = State.GetCreature(attacker);
        if (attackerState.IsDead)
        {
            return;
        }

        if (!attackCommand.IsSingleTargeted && !attackCommand.IsMultiTargeted)
        {
            Entry.Logger.Warn("AttackCommand prediction skipped: command has no targets configured.");
            History.RecordRisk(PredictionRiskReason.MethodMirrorIncomplete);
            return;
        }

        HookMirrors.BeforeAttack(this, attackCommand);

        var hitCount = HookMirrors.ModifyAttackHitCount(this, attackCommand, attackCommand._hitCount);

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

            var singleTarget = SelectSingleAttackTarget(attackCommand, validTargets);
            if (attackCommand.IsRandomlyTargeted && singleTarget == null)
            {
                break;
            }

            var results = Damage(
                singleTarget != null ? [singleTarget] : validTargets,
                GetAttackDamageAmount(attackCommand, cardSource, singleTarget),
                attackCommand.DamageProps,
                attacker,
                cardSource,
                attackCommand.CardPlay);
            attackCommand.AddResultsInternal(results);
        }

        History.CreatureAttacked(
            attacker,
            attackCommand.Results.SelectMany(results => results).ToArray());

        HookMirrors.AfterAttack(this, attackCommand);
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

    private Creature? SelectSingleAttackTarget(AttackCommand attackCommand, List<Creature> validTargets)
    {
        if (!attackCommand.IsRandomlyTargeted)
        {
            return validTargets.Count == 1 ? validTargets[0] : null;
        }

        if (!attackCommand._doesRandomTargetingAllowDuplicates)
        {
            var previousReceivers = attackCommand.Results
                .SelectMany(results => results)
                .Select(result => result.Receiver)
                .ToHashSet();
            validTargets = validTargets
                .Where(creature => !previousReceivers.Contains(creature))
                .ToList();

            if (validTargets.Count == 0)
            {
                Entry.Logger.Warn("No valid targets available for randomly-targeted attack.");
                History.RecordRisk(PredictionRiskReason.MethodMirrorIncomplete);
                return null;
            }
        }

        return Rng.CombatTargets.NextItem(validTargets);
    }

    private decimal GetAttackDamageAmount(
        AttackCommand attackCommand,
        PredictedCard? cardSource,
        Creature? singleTarget)
    {
        if (attackCommand._calculatedDamageVar is {} calculatedDamageVar)
        {
            if (cardSource is null)
            {
                throw new InvalidOperationException("CalculatedDamage simulation requires a card source.");
            }

            return calculatedDamageVar.InvokeCalculate(this, cardSource, singleTarget);
        }

        return attackCommand._damagePerHit;
    }
}
