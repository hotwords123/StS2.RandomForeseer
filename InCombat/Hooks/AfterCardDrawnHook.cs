using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Afflictions;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Random;
using RandomForeseer.Common;
using RandomForeseer.Common.Hooks;
using Cards = MegaCrit.Sts2.Core.Models.Cards;

namespace RandomForeseer.InCombat.Hooks;

// Mirrors the prediction-relevant parts of Hook.AfterCardDrawn.
internal static class AfterCardDrawnHook
{
    private static readonly HookSpec AfterCardDrawnEarly = new(
        nameof(AbstractModel.AfterCardDrawnEarly),
        [
            typeof(PlayerChoiceContext),
            typeof(CardModel),
            typeof(bool)
        ]);

    private static readonly HookSpec AfterCardDrawn = new(
        nameof(AbstractModel.AfterCardDrawn),
        [
            typeof(PlayerChoiceContext),
            typeof(CardModel),
            typeof(bool)
        ]);

    private static readonly HookRegistry<AfterCardDrawnHookContext> Early = CreateEarly();
    private static readonly HookRegistry<AfterCardDrawnHookContext> Normal = CreateNormal();

    public static void RunEarly(AfterCardDrawnHookContext context)
    {
        Early.Run(context.CombatState.IterateHookListeners(), context);
    }

    public static void Run(AfterCardDrawnHookContext context)
    {
        Normal.Run(context.CombatState.IterateHookListeners(), context);
    }

    private static HookRegistry<AfterCardDrawnHookContext> CreateEarly()
    {
        var registry = new HookRegistry<AfterCardDrawnHookContext>(AfterCardDrawnEarly);

        registry.Register<HellraiserPower>(HandleHellraiserPower);

        return registry;
    }

    private static HookRegistry<AfterCardDrawnHookContext> CreateNormal()
    {
        var registry = new HookRegistry<AfterCardDrawnHookContext>(AfterCardDrawn);

        registry.Register<ConfusedPower>(HandleConfusedPower);
        registry.Register<Slither>(HandleSlither);
        registry.Register<IterationPower>(HandleIterationPower);
        registry.Register<PagestormPower>(HandlePagestormPower);
        registry.Register<ChainsOfBindingPower>(HandleChainsOfBindingPower);
        registry.Register<CorrosiveWavePower>(HandleDriftRiskIfOwner);
        registry.Register<SpeedsterPower>(HandleSpeedsterPower);
        registry.Register<KinglyKick>(HandleKinglyKick);
        registry.Register<KinglyPunch>(HandleKinglyPunch);

        registry.RegisterIgnored<AutomationPower>();
        registry.RegisterIgnored<Cards.Void>();

        return registry;
    }

    private static void HandleHellraiserPower(HellraiserPower power, AfterCardDrawnHookContext context)
    {
        // Hellraiser auto-plays the drawn Strike; arbitrary auto-play command effects are
        // not mirrored here.
        if (context.PreviewCard.Owner.Creature == power.Owner && context.PreviewCard.Tags.Contains(CardTag.Strike))
        {
            context.RiskTracker.AddCurrentSource();
        }
    }

    private static void HandleConfusedPower(ConfusedPower power, AfterCardDrawnHookContext context)
    {
        if (context.PreviewCard.Owner != power.Owner?.Player || context.PreviewCard.EnergyCost.Canonical < 0)
        {
            return;
        }

        context.MutablePreviewCard.EnergyCost.SetThisCombat(context.EnergyCostRng.NextInt(4));
    }

    private static void HandleSlither(Slither slither, AfterCardDrawnHookContext context)
    {
        if (context.OriginalCard != slither.Card)
        {
            return;
        }

        context.MutablePreviewCard.EnergyCost.SetThisCombat(context.EnergyCostRng.NextInt(4));
    }

    private static void HandleIterationPower(IterationPower power, AfterCardDrawnHookContext context)
    {
        if (context.PreviewCard.Owner.Creature != power.Owner ||
            context.PreviewCard.Type != CardType.Status ||
            context.State.StatusCardsDrawnThisTurn > 1)
        {
            return;
        }

        context.Draw(power.Amount);
    }

    private static void HandlePagestormPower(PagestormPower power, AfterCardDrawnHookContext context)
    {
        if (context.PreviewCard.Owner.Creature != power.Owner || !context.PreviewCard.Keywords.Contains(CardKeyword.Ethereal))
        {
            return;
        }

        context.Draw(power.Amount);
    }

    private static void HandleChainsOfBindingPower(ChainsOfBindingPower power, AfterCardDrawnHookContext context)
    {
        var bound = ModelDb.Affliction<Bound>();
        if (context.PreviewCard.Owner != power.Owner?.Player ||
            context.CombatState.CurrentSide != power.Owner.Side ||
            !bound.CanAfflict(context.PreviewCard) ||
            context.State.BoundCardsAfflictedThisTurn >= power.Amount)
        {
            return;
        }

        context.MutablePreviewCard.AfflictInternal(bound.ToMutable(), power.Amount);
        context.State.BoundCardsAfflictedThisTurn++;
    }

    private static void HandleSpeedsterPower(SpeedsterPower power, AfterCardDrawnHookContext context)
    {
        // Speedster deals damage to all hittable enemies after a non-hand draw on the owner's
        // turn; damage side effects are not mirrored here.
        if (!context.FromHandDraw &&
            context.PreviewCard.Owner.Creature == power.Owner &&
            context.PreviewCard.Owner.Creature.CombatState?.CurrentSide == context.PreviewCard.Owner.Creature.Side)
        {
            context.RiskTracker.AddCurrentSource();
        }
    }

    private static void HandleKinglyKick(KinglyKick card, AfterCardDrawnHookContext context)
    {
        if (context.OriginalCard != card)
        {
            return;
        }

        context.MutablePreviewCard.EnergyCost.AddThisCombat(-1);
    }

    private static void HandleKinglyPunch(KinglyPunch card, AfterCardDrawnHookContext context)
    {
        if (context.OriginalCard != card)
        {
            return;
        }

        var previewCard = context.MutablePreviewCard;
        previewCard.DynamicVars.Damage.BaseValue += previewCard.DynamicVars["Increase"].BaseValue;
    }

    private static void HandleDriftRiskIfOwner(PowerModel power, AfterCardDrawnHookContext context)
    {
        // CorrosiveWavePower applies Poison to all hittable enemies; power application
        // side effects are not mirrored here.
        if (context.PreviewCard.Owner.Creature == power.Owner)
        {
            context.RiskTracker.AddCurrentSource();
        }
    }

}

internal sealed class AfterCardDrawnHookContext : IPredictionHookContext
{
    public required PredictionRiskTracker RiskTracker { get; init; }

    public required ICombatState CombatState { get; init; }

    public required Player Player { get; init; }

    public required PredictedCard Card { get; init; }

    public CardModel OriginalCard => Card.Original;

    public CardModel PreviewCard => Card.Preview;

    public CardModel MutablePreviewCard => Card.MutablePreview;

    public required bool FromHandDraw { get; init; }

    public required Rng EnergyCostRng { get; init; }

    public required DrawPilePredictionState State { get; init; }

    public required Action<int> Draw { get; init; }
}
