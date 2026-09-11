using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.InCombat.Mirrors;
using RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Hooks;
using RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Hooks.Power;

namespace RandomForeseer.RandomForeseerCode.InCombat.Simulation;

internal sealed partial class CombatPredictionSimulator
{
    /// <summary>
    /// Mirrors <see cref="PowerCmd.Apply{T}(PlayerChoiceContext, IEnumerable{Creature}, decimal, Creature, CardModel, bool)"/>.
    /// </summary>
    public IReadOnlyList<T> ApplyPower<T>(
        IReadOnlyList<Creature> targets,
        decimal amount,
        Creature? applier,
        PredictedCard? cardSource)
        where T : PowerModel
    {
        List<T> powers = [];

        foreach (var target in targets)
        {
            if (ApplyPower<T>(target, amount, applier, cardSource) is { } power)
            {
                powers.Add(power);
            }
        }

        return powers;
    }

    /// <summary>
    /// Mirrors <see cref="PowerCmd.Apply{T}(PlayerChoiceContext, Creature, decimal, Creature, CardModel, bool)"/>.
    /// </summary>
    public T? ApplyPower<T>(Creature target, decimal amount, Creature? applier, PredictedCard? cardSource)
        where T : PowerModel
    {
        if (IsEnding || !State.GetCreature(target).CanReceivePowers)
        {
            return null;
        }

        var canonicalPower = ModelDb.Power<T>();
        var power = PowerCmd.FindExistingInstanceForStacking(canonicalPower, target, applier);
        if (power is null)
        {
            power = canonicalPower.ToMutable();
            ApplyPower(power, target, amount, applier, cardSource);
        }
        else if (ModifyPowerAmount(power, amount, applier, cardSource) == 0)
        {
            power = null;
        }

        return power as T;
    }

    /// <summary>
    /// Mirrors <see cref="PowerCmd.Apply(PlayerChoiceContext, PowerModel, Creature, decimal, Creature?, CardModel?, bool)"/>.
    /// </summary>
    /// <remarks>The supplied power must be a detached, prediction-owned mutable instance.</remarks>
    public void ApplyPower(
        PowerModel power,
        Creature target,
        decimal amount,
        Creature? applier,
        PredictedCard? cardSource)
    {
        if (IsEnding || amount == 0m || !State.GetCreature(target).CanReceivePowers)
        {
            return;
        }

        var existingPower = PowerCmd.FindExistingInstanceForStacking(power, target, applier);
        if (existingPower is not null)
        {
            ModifyPowerAmount(existingPower, amount, applier, cardSource);
            return;
        }

        power.AssertMutable();
        power.Applier = applier;
        var modifiedAmount = ResolvePowerAmountChange(
            power, target, amount, applier, cardSource, out var givenModifiers, out var receivedModifiers);

        if (State.Players.Count > 1 && target.IsEnemy && power.ShouldScaleInMultiplayer)
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
        PowerApplicationMirrors.InvokeBefore(power, applicationContext);

        if (!State.GetCreature(target).CanReceivePowers)
        {
            return;
        }

        // PowerModel.ApplyInternal would set Owner/Amount and mutate the live power collection.
        // Power state, PowerReceived history and SkipNextDurationTick remain unmodeled; see docs/mirrors/power-apply.md.
        if (modifiedAmount != 0m)
        {
            History.RecordRisk(PredictionRiskReason.MethodMirrorIncomplete);
        }

        AfterModifyingPowerAmount(power, givenModifiers, receivedModifiers);

        if (modifiedAmount == 0m)
        {
            return;
        }

        PowerApplicationMirrors.InvokeAfter(power, applicationContext);
        HookMirrors.AfterPowerAmountChanged(this, power, modifiedAmount, target, applier, cardSource);
    }

    /// <summary>
    /// Mirrors <see cref="PowerCmd.ModifyAmount"/>.
    /// </summary>
    /// <remarks>Updates the shadow amount and returns the pre-clamp result; power removal remains unmodeled.</remarks>
    public int ModifyPowerAmount(PowerModel power, decimal offset, Creature? applier, PredictedCard? cardSource)
    {
        if (IsEnding)
        {
            return 0;
        }

        var owner = power.Owner;
        if (!State.Creatures.Contains(owner))
        {
            return 0;
        }

        var modifiedOffset = ResolvePowerAmountChange(
            power, owner, offset, applier, cardSource, out var givenModifiers, out var receivedModifiers);

        // Mirror PowerModel.SetAmount in shadow state without firing live model events.
        // PowerReceived history, removal and value hooks that read live amounts remain unmodeled.
        var powerState = StateStore.GetPowerAmount(power);
        var newAmount = powerState.Amount + (int)modifiedOffset;
        powerState.Amount = Math.Clamp(newAmount, -999999999, 999999999);
        if ((int)modifiedOffset != 0)
        {
            History.RecordRisk(PredictionRiskReason.MethodMirrorIncomplete);
        }

        AfterModifyingPowerAmount(power, givenModifiers, receivedModifiers);
        if ((int)modifiedOffset != 0)
        {
            HookMirrors.AfterPowerAmountChanged(this, power, modifiedOffset, owner, applier, cardSource);
        }

        return newAmount;
    }

    private decimal ResolvePowerAmountChange(
        PowerModel power,
        Creature target,
        decimal amount,
        Creature? applier,
        PredictedCard? cardSource,
        out List<AbstractModel>? givenModifiers,
        out List<AbstractModel> receivedModifiers)
    {
        HookMirrors.BeforePowerAmountChanged(this, power, amount, target, applier, cardSource);

        givenModifiers = null;
        if (applier is not null && State.Creatures.Contains(applier))
        {
            amount = HookMirrors.ModifyPowerAmountGiven(
                this, power, applier, amount, target, cardSource, out givenModifiers);
        }

        amount = HookMirrors.ModifyPowerAmountReceived(
            this, power, target, amount, applier, out receivedModifiers);
        return amount;
    }

    private void AfterModifyingPowerAmount(
        PowerModel power,
        List<AbstractModel>? givenModifiers,
        List<AbstractModel> receivedModifiers)
    {
        if (givenModifiers is not null)
        {
            HookMirrors.AfterModifyingPowerAmountGiven(this, givenModifiers, power);
        }

        HookMirrors.AfterModifyingPowerAmountReceived(this, receivedModifiers, power);
    }
}
