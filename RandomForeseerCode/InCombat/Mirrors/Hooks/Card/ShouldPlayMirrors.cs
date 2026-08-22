using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Afflictions;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.Common.Mirrors;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Hooks.Card;

using Registry = MethodMirrorRegistry<AbstractModel, ShouldPlayMirrorContext, bool>;

// Mirrors Hook.ShouldPlay while selectively replacing listeners that read card-play state.
internal static class ShouldPlayMirrors
{
    private static readonly MirrorMethodSpec ShouldPlay = MirrorMethodSpec.Hook(
        nameof(AbstractModel.ShouldPlay),
        [typeof(CardModel), typeof(AutoPlayType)]);

    private static readonly Registry Registry = CreateRegistry();

    public static bool Invoke(AbstractModel listener, ShouldPlayMirrorContext context)
    {
        if (Registry.TryInvokeRegistered(listener, context, out var result))
        {
            return result.Value;
        }

        return listener.ShouldPlay(context.Card.Preview, context.AutoPlayType);
    }

    private static Registry CreateRegistry()
    {
        var registry = new Registry(ShouldPlay);

        registry.Register<ChainsOfBindingPower>(HandleChainsOfBindingPower);
        registry.Register<SlothPower>(HandleSlothPower);

        registry.Register<VelvetChoker>(HandleVelvetChoker);

        return registry;
    }

    private static bool HandleChainsOfBindingPower(
        ChainsOfBindingPower power,
        ShouldPlayMirrorContext context)
    {
        return context.Card.Preview.Owner.Creature != power.Owner ||
            context.Card.Preview.Affliction is not Bound ||
            !context.StateStore.Get(power, () => new ChainsOfBindingPredictionState(power)).BoundCardPlayed;
    }

    private static bool HandleSlothPower(SlothPower power, ShouldPlayMirrorContext context)
    {
        return context.Card.Preview.Owner.Creature != power.Owner ||
            context.StateStore.Get(power, () => new CounterPredictionState(power._cardsPlayedThisTurn)).Value <
            power.Amount;
    }

    private static bool HandleVelvetChoker(VelvetChoker relic, ShouldPlayMirrorContext context)
    {
        return context.Card.Preview.Owner != relic.Owner ||
            context.StateStore.Get(relic, () => new CounterPredictionState(relic._cardsPlayedThisTurn)).Value <
            relic.DynamicVars.Cards.IntValue;
    }
}

internal sealed class ShouldPlayMirrorContext : CombatMirrorContext
{
    public required PredictedCard Card { get; init; }

    public required AutoPlayType AutoPlayType { get; init; }
}
