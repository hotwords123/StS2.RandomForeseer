using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.Common.Mirrors;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Hooks.Damage;

using Registry = ModelMethodMirrorRegistry<AbstractModel, BeforeDamageReceivedMirrorContext>;

// Mirrors the prediction-relevant parts of Hook.BeforeDamageReceived.
internal static class BeforeDamageReceivedMirrors
{
    private static readonly MirrorMethodSpec BeforeDamageReceived = MirrorMethodSpec.Hook(
        nameof(AbstractModel.BeforeDamageReceived),
        [
            typeof(PlayerChoiceContext),
            typeof(Creature),
            typeof(decimal),
            typeof(ValueProp),
            typeof(Creature),
            typeof(CardModel)
        ]);

    private static readonly Registry Registry = CreateRegistry();

    public static void Invoke(AbstractModel listener, BeforeDamageReceivedMirrorContext context)
    {
        Registry.Invoke(listener, context);
    }

    private static Registry CreateRegistry()
    {
        var registry = new Registry(BeforeDamageReceived);

        registry.Register<ThornsPower>(HandleThornsPower);

        return registry;
    }

    private static void HandleThornsPower(ThornsPower power, BeforeDamageReceivedMirrorContext context)
    {
        if (context.Target == power.Owner &&
            context.Dealer != null &&
            (context.Props.IsPoweredAttack() || context.Source?.Original is Omnislice))
        {
            context.Simulator.Damage(
                context.Dealer,
                power.Amount,
                ValueProp.Unpowered | ValueProp.SkipHurtAnim,
                power.Owner);
        }
    }
}

internal sealed class BeforeDamageReceivedMirrorContext : CombatPredictionMirrorContext
{
    public required Creature Target { get; init; }

    public required decimal Amount { get; init; }

    public required ValueProp Props { get; init; }

    public required Creature? Dealer { get; init; }

    public required PredictedCard? Source { get; init; }
}
