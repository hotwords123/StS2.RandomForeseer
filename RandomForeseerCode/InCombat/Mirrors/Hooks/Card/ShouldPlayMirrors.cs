using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Afflictions;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.Common.Mirrors;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;

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

        registry.Register<Enthralled>(HandleEnthralled);
        registry.Register<Normality>(HandleNormality);

        registry.Register<ChainsOfBindingPower>(HandleChainsOfBindingPower);
        registry.Register<RingingPower>(HandleRingingPower);
        registry.Register<SlothPower>(HandleSlothPower);

        registry.Register<VelvetChoker>(HandleVelvetChoker);

        return registry;
    }

    private static bool HandleEnthralled(Enthralled card, ShouldPlayMirrorContext context)
    {
        return context.Card.Preview.Owner != card.Owner ||
            context.AutoPlayType != AutoPlayType.None ||
            context.Card.Preview is Enthralled ||
            context.State.FindCard(card)?.GetPile(context.State)?.Type != PileType.Hand;
    }

    private static bool HandleNormality(Normality card, ShouldPlayMirrorContext context)
    {
        return context.Card.Preview.Owner != card.Owner ||
            context.State.FindCard(card)?.GetPile(context.State)?.Type != PileType.Hand ||
            CountCardPlaysStartedThisTurn(card.Owner, context) < 3;
    }

    private static bool HandleChainsOfBindingPower(
        ChainsOfBindingPower power,
        ShouldPlayMirrorContext context)
    {
        return context.Card.Preview.Owner.Creature != power.Owner ||
            context.Card.Preview.Affliction is not Bound ||
            !context.StateStore.Get(power, () => new ChainsOfBindingPredictionState(power)).BoundCardPlayed;
    }

    private static bool HandleRingingPower(RingingPower power, ShouldPlayMirrorContext context)
    {
        return context.Card.Preview.Owner.Creature != power.Owner ||
            context.Card.Preview.Affliction is not Ringing ||
            CountCardPlaysStartedThisTurn(context.Card.Preview.Owner, context) == 0;
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

    private static int CountCardPlaysStartedThisTurn(Player player, ShouldPlayMirrorContext context)
    {
        return CombatManager.Instance.History.CardPlaysStarted.Count(entry =>
                entry.HappenedThisTurn(context.CombatState) && entry.CardPlay.Player == player) +
            context.History.OfType<CombatPredictionCardPlayStartedEntry>().Count(entry =>
                entry.CardPlay.Player == player);
    }
}

internal sealed class ShouldPlayMirrorContext : CombatMirrorContext
{
    public required PredictedCard Card { get; init; }

    public required AutoPlayType AutoPlayType { get; init; }
}
