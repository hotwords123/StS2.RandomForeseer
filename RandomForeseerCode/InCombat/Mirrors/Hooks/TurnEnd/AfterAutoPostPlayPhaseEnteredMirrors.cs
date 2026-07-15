using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using RandomForeseer.RandomForeseerCode.Common.Mirrors;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Hooks.TurnEnd;

using Registry = ModelMethodMirrorRegistry<AbstractModel, AfterAutoPostPlayMirrorContext>;

// Mirrors the prediction-relevant parts of Hook.AfterAutoPostPlayPhaseEntered.
internal static class AfterAutoPostPlayPhaseEnteredMirrors
{
    private static readonly MirrorMethodSpec AfterAutoPostPlayPhaseEntered = MirrorMethodSpec.Hook(
        nameof(AbstractModel.AfterAutoPostPlayPhaseEntered),
        [typeof(PlayerChoiceContext), typeof(Player)]);

    private static readonly Registry Registry = CreateRegistry();

    public static void Invoke(
        AbstractModel listener,
        AfterAutoPostPlayMirrorContext context)
    {
        Registry.Invoke(listener, context);
    }

    private static Registry CreateRegistry()
    {
        var registry = new Registry(AfterAutoPostPlayPhaseEntered);

        registry.Register<HowlFromBeyond>(HandleHowlFromBeyond);
        registry.Register<IAmInvincible>(HandleIAmInvincible);
        registry.Register<StampedePower>(HandleStampedePower);

        return registry;
    }

    private static void HandleHowlFromBeyond(HowlFromBeyond card, AfterAutoPostPlayMirrorContext context)
    {
        if (context.Player != card.Owner ||
            context.State.GetPlayerCombatState(card.Owner).ExhaustPile.Find(card) is not { } predictedCard)
        {
            return;
        }

        context.Simulator.AutoPlay(predictedCard);
    }

    private static void HandleIAmInvincible(IAmInvincible card, AfterAutoPostPlayMirrorContext context)
    {
        if (context.Player != card.Owner ||
            context.State.GetPlayerCombatState(card.Owner).DrawPile.TopCard?.References(card) is not true)
        {
            return;
        }

        context.Simulator.AutoPlayFromDrawPile(card.Owner, 1, CardPilePosition.Top);
    }

    private static void HandleStampedePower(StampedePower power, AfterAutoPostPlayMirrorContext context)
    {
        if (context.Player.Creature != power.Owner)
        {
            return;
        }

        var hand = context.State.GetPlayerCombatState(context.Player).Hand;

        for (var i = 0; i < power.Amount; i++)
        {
            var candidates = hand.Cards
                .Where(card =>
                    card.Preview.Type == CardType.Attack &&
                    !card.GetKeywords(context.State).Contains(CardKeyword.Unplayable))
                .ToList();
            var card = context.Rng.Shuffle.NextItem(candidates);

            if (card != null)
            {
                context.Simulator.AutoPlay(card);
            }
        }
    }
}

internal sealed class AfterAutoPostPlayMirrorContext : CombatPredictionMirrorContext
{
    public required Player Player { get; init; }
}
