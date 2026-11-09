using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using RandomForeseer.InCombat.Mirrors.Hooks.Damage;

namespace RandomForeseer.InCombat.Mirrors.Hooks.Attack;

internal static class GigantificationPowerMirrors
{
    public static void BeforeAttack(GigantificationPower power, BeforeAttackMirrorContext context)
    {
        if (context.StateStore.GetPowerAmount(power).IsActive && ShouldTrigger(power, context.Command))
        {
            GetState(power, context).CommandToModify ??= context.Command;
        }
    }

    public static void AfterAttack(GigantificationPower power, AfterAttackMirrorContext context)
    {
        var state = GetState(power, context);
        if (context.Command == state.CommandToModify)
        {
            state.CommandToModify = null;
            context.StateStore.GetPowerAmount(power).Decrement();
        }
    }

    public static decimal ModifyDamageMultiplicative(
        GigantificationPower power,
        ModifyDamageMirrorContext context)
    {
        if (!context.StateStore.GetPowerAmount(power).IsActive ||
            context.CardSource is null ||
            context.CardSource.Preview.Owner.Creature != power.Owner ||
            !context.Props.IsPoweredAttack())
        {
            return 1;
        }

        var commandToModify = GetState(power, context).CommandToModify;
        return commandToModify is null || context.CardSource.References(commandToModify.ModelSource) ? 3 : 1;
    }

    private static State GetState(GigantificationPower power, CombatMirrorContext context)
    {
        return context.StateStore.Get<State>(power);
    }

    private static bool ShouldTrigger(GigantificationPower power, AttackCommand command)
    {
        return command.ModelSource is CardModel card &&
            card.Owner.Creature == power.Owner &&
            card.Type == CardType.Attack &&
            command.DamageProps.IsPoweredAttack();
    }

    private sealed class State
    {
        public AttackCommand? CommandToModify { get; set; }
    }
}
