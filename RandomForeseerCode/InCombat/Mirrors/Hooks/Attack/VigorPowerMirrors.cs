using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using RandomForeseer.RandomForeseerCode.Common;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Hooks.Attack;

internal static class VigorPowerMirrors
{
    public static void BeforeAttack(VigorPower power, BeforeAttackMirrorContext context)
    {
        if (ShouldTrigger(power, context.Command))
        {
            var state = GetState(power, context);
            state.CommandToModify ??= context.Command;
        }
    }

    public static void AfterAttack(VigorPower power, AfterAttackMirrorContext context)
    {
        var state = GetState(power, context);
        if (context.Command == state.CommandToModify)
        {
            // Vanilla does not clear the command to modify here, which may be a bug.
            context.History.RecordRisk(PredictionRiskReason.MethodMirrorIncomplete);
        }
    }

    private static State GetState(VigorPower power, CombatPredictionMirrorContext context)
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
    }
}
