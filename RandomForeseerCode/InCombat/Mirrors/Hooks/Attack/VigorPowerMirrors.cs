using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using RandomForeseer.InCombat.Mirrors.Hooks.Damage;

namespace RandomForeseer.InCombat.Mirrors.Hooks.Attack;

internal static class VigorPowerMirrors
{
    public static void BeforeAttack(VigorPower power, BeforeAttackMirrorContext context)
    {
        var amountState = context.StateStore.GetPowerAmount(power);
        if (amountState.IsActive && ShouldTrigger(power, context.Command))
        {
            var state = GetState(power, context);
            if (state.CommandToModify is null)
            {
                state.CommandToModify = context.Command;
                state.AmountWhenAttackStarted = amountState.Amount;
            }
        }
    }

    public static void AfterAttack(VigorPower power, AfterAttackMirrorContext context)
    {
        var state = GetState(power, context);
        if (context.Command == state.CommandToModify)
        {
            context.StateStore.GetPowerAmount(power).Decrease(state.AmountWhenAttackStarted);
            state.CommandToModify = null;
            state.AmountWhenAttackStarted = 0;
        }
    }

    public static decimal ModifyDamageAdditive(VigorPower power, ModifyDamageMirrorContext context)
    {
        var amountState = context.StateStore.GetPowerAmount(power);
        if (!amountState.IsActive || context.Dealer != power.Owner || !context.Props.IsPoweredAttack())
        {
            return 0;
        }

        var commandToModify = GetState(power, context).CommandToModify;
        if (commandToModify is not null &&
            context.CardSource is not null &&
            !context.CardSource.References(commandToModify.ModelSource))
        {
            return 0;
        }

        if (commandToModify is not null && commandToModify.Attacker != context.Dealer)
        {
            return 0;
        }

        return amountState.Amount;
    }

    private static State GetState(VigorPower power, CombatMirrorContext context)
    {
        return context.StateStore.Get<State>(power);
    }

    private static bool ShouldTrigger(VigorPower power, AttackCommand command)
    {
        return command.Attacker == power.Owner &&
            command.DamageProps.IsPoweredAttack() &&
            command.ModelSource is null or CardModel;
    }

    private sealed class State
    {
        public AttackCommand? CommandToModify { get; set; }

        public int AmountWhenAttackStarted { get; set; }
    }
}
