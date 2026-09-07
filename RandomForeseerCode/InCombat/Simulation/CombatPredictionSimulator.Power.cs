using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.InCombat.Mirrors;
using RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Hooks.Power;

namespace RandomForeseer.RandomForeseerCode.InCombat.Simulation;

internal sealed partial class CombatPredictionSimulator
{
    /// <summary>
    /// Mirrors the prediction-relevant Hook and model-lifecycle boundary of <see cref="PowerCmd.Apply{T}"/> without
    /// mutating the live creature power collection.
    /// </summary>
    public void ApplyPower<T>(
        IEnumerable<Creature>? targets,
        decimal amount,
        Creature? applier,
        PredictedCard? cardSource)
        where T : PowerModel
    {
        if (targets is null)
        {
            return;
        }

        var canonicalPower = ModelDb.Power<T>();
        foreach (var target in targets.ToList())
        {
            ApplyPower(canonicalPower, target, amount, applier, cardSource);
        }
    }

    private void ApplyPower(
        PowerModel canonicalPower,
        Creature target,
        decimal amount,
        Creature? applier,
        PredictedCard? cardSource)
    {
        if (IsOverOrEnding || amount == 0m || !State.GetCreature(target).IsAlive || !target.CanReceivePowers)
        {
            return;
        }

        var existingPower = PowerCmd.FindExistingInstanceForStacking(canonicalPower, target, applier);
        var power = existingPower ?? canonicalPower;

        HookMirrors.BeforePowerAmountChanged(this, power, amount, target, applier, cardSource);

        var modifiedAmount = amount;
        List<AbstractModel> givenModifiers = [];
        if (applier is not null && State.Creatures.Contains(applier))
        {
            modifiedAmount = HookMirrors.ModifyPowerAmountGiven(
                this,
                power,
                applier,
                modifiedAmount,
                target,
                cardSource,
                out givenModifiers);
        }

        modifiedAmount = HookMirrors.ModifyPowerAmountReceived(
            this,
            power,
            target,
            modifiedAmount,
            applier,
            out var receivedModifiers);

        if (State.Players.Count > 1 &&
            (target.IsPrimaryEnemy || target.IsSecondaryEnemy) &&
            power.ShouldScaleInMultiplayer)
        {
            modifiedAmount = power.GetScaledAmountForMultiplayer(
                State.CombatState,
                applier,
                modifiedAmount,
                target,
                cardSource?.Preview);
        }

        var applicationContext = new PowerApplicationMirrorContext
        {
            Simulator = this,
            Target = target,
            Amount = modifiedAmount,
            Applier = applier,
            CardSource = cardSource
        };
        if (existingPower is null)
        {
            PowerApplicationMirrors.InvokeBefore(power, applicationContext);
        }

        if (!State.GetCreature(target).IsAlive || !target.CanReceivePowers)
        {
            return;
        }

        HookMirrors.AfterModifyingPowerAmountGiven(this, givenModifiers, power);
        HookMirrors.AfterModifyingPowerAmountReceived(this, receivedModifiers, power);

        if (modifiedAmount == 0m)
        {
            return;
        }

        if (existingPower is null)
        {
            PowerApplicationMirrors.InvokeAfter(power, applicationContext);
        }

        HookMirrors.AfterPowerAmountChanged(this, power, modifiedAmount, target, applier, cardSource);
    }
}
