using System.Reflection;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.ValueProps;
using RandomForeseer.Common;
using RandomForeseer.Common.Mirrors;
using RandomForeseer.InCombat.Simulation;

namespace RandomForeseer.InCombat.Mirrors.Cards;

using Registry = MethodMirrorRegistry<CardModel, CardOnTurnEndInHandMirrorContext>;

// Simulation-facing facade and central registration index for mirrored CardModel.OnTurnEndInHand behavior.
internal static class CardOnTurnEndInHandMirrors
{
    private static readonly MirrorMethodSpec OnTurnEndInHand = new(
        typeof(CardModel),
        "OnTurnEndInHand",
        BindingFlags.Instance | BindingFlags.NonPublic,
        [typeof(PlayerChoiceContext)]);

    private static readonly Registry Registry = CreateRegistry();

    public static MirrorDispatchResult Invoke(CombatPredictionSimulator simulator, PredictedCard card)
    {
        // Do not force a clone for read-only handlers. Regret's BeforeSideTurnEnd mirror creates its mutable preview
        // before this dispatch because that override reads and resets card-local state.
        return Registry.Invoke(card.Preview, new()
        {
            Simulator = simulator,
            Card = card
        });
    }

    private static Registry CreateRegistry()
    {
        var registry = new Registry(OnTurnEndInHand);

        registry.Register<BadLuck>(HandleHpLoss);
        registry.Register<Beckon>(HandleHpLoss);

        registry.Register<Burn>(HandleDamage);
        registry.Register<Decay>(HandleDamage);
        registry.Register<Infection>(HandleDamage);
        registry.Register<Toxic>(HandleDamage);
        registry.Register<Wither>(HandleDamage);

        registry.Register<Regret>(HandleRegret);

        // Debt only removes run gold, which is not consumed by combat prediction.
        registry.RegisterIgnored<Debt>();
        // Doubt and Shame cannot affect the remaining phase-one damage; their debuffs matter from the next player
        // turn onward, outside this prediction surface.
        registry.RegisterIgnored<Doubt>();
        registry.RegisterIgnored<Shame>();

        return registry;
    }

    private static void HandleDamage(CardModel card, CardOnTurnEndInHandMirrorContext context)
    {
        DamageOwner(context, card.DynamicVars.Damage.BaseValue, card.DynamicVars.Damage.Props);
    }

    private static void HandleHpLoss(CardModel card, CardOnTurnEndInHandMirrorContext context)
    {
        DamageOwner(context, card.DynamicVars.HpLoss.BaseValue, DamageProps.cardHpLoss);
    }

    private static void HandleRegret(Regret card, CardOnTurnEndInHandMirrorContext context)
    {
        var previewCard = (Regret)context.MutablePreviewCard;
        DamageOwner(context, previewCard.CardsInHand, DamageProps.cardHpLoss);
        previewCard.CardsInHand = 0;
    }

    /// <summary>
    /// Mirrors the shared damage behavior for turn-end-in-hand cards.
    /// </summary>
    private static void DamageOwner(CardOnTurnEndInHandMirrorContext context, decimal amount, ValueProp props)
    {
        var owner = context.PreviewCard.Owner.Creature;
        context.Simulator.Damage([owner], amount, props, owner, context.Card, cardPlay: null);
    }
}

internal sealed class CardOnTurnEndInHandMirrorContext : CombatCardMirrorContext<CardModel>
{
    // The dispatch trace belongs to the real card, not its optional detached preview.
    protected override AbstractModel GetDispatchSource(CardModel _) => OriginalCard;
}
