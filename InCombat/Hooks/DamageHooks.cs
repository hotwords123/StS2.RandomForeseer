using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using RandomForeseer.Common.Hooks;

namespace RandomForeseer.InCombat.Hooks;

internal static class DamageHooks
{
    private static readonly HookSpec BeforeDamageReceived = new(
        nameof(AbstractModel.BeforeDamageReceived),
        [
            typeof(PlayerChoiceContext),
            typeof(Creature),
            typeof(decimal),
            typeof(ValueProp),
            typeof(Creature),
            typeof(CardModel)
        ]);

    private static readonly HookRegistry<BeforeDamageReceivedHookContext> BeforeDamageReceivedRegistry =
        CreateBeforeDamageReceivedRegistry();

    public static void RunBeforeDamageReceived(BeforeDamageReceivedHookContext context)
    {
        BeforeDamageReceivedRegistry.Run(context.RunState.IterateHookListeners(context.CombatState), context);
    }

    private static HookRegistry<BeforeDamageReceivedHookContext> CreateBeforeDamageReceivedRegistry()
    {
        var registry = new HookRegistry<BeforeDamageReceivedHookContext>(BeforeDamageReceived);

        registry.Register<ThornsPower>(HandleThornsPower);

        return registry;
    }

    private static void HandleThornsPower(ThornsPower power, BeforeDamageReceivedHookContext context)
    {
        if (context.Target != power.Owner ||
            context.Dealer == null ||
            (!context.Props.IsPoweredAttack() && context.Source is not Omnislice))
        {
            return;
        }

        context.Simulator.Damage(
            context.Dealer,
            power.Amount,
            ValueProp.Unpowered | ValueProp.SkipHurtAnim,
            power.Owner);
    }
}

internal sealed class BeforeDamageReceivedHookContext : CombatPredictionHookContext
{
    public required Creature Target { get; init; }

    public required ValueProp Props { get; init; }

    public required Creature? Dealer { get; init; }

    public required CardModel? Source { get; init; }
}
