using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Hooks.Attack;

internal static class GigantificationPowerMirrors
{
    public static void BeforeAttack(GigantificationPower power, BeforeAttackMirrorContext context)
    {
        if (ShouldTrigger(power, context.Command))
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
            context.MarkCurrentSourceRisky();
        }
    }

    private static State GetState(GigantificationPower power, CombatPredictionMirrorContext context)
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
