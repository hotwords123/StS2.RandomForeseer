using System.Runtime.CompilerServices;
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

    public static IReadOnlyList<HookResult> RunEarly(AfterCardDrawnHookContext context)
    {
        return Early.Run(context.CombatState.IterateHookListeners(), context);
    }

    public static IReadOnlyList<HookResult> Run(AfterCardDrawnHookContext context)
    {
        return Normal.Run(context.CombatState.IterateHookListeners(), context);
    }

    private static HookRegistry<AfterCardDrawnHookContext> CreateEarly()
    {
        var registry = new HookRegistry<AfterCardDrawnHookContext>(AfterCardDrawnEarly, "Draw-pile prediction");

        registry.Register<HellraiserPower>(HandleHellraiserPower);

        return registry;
    }

    private static HookRegistry<AfterCardDrawnHookContext> CreateNormal()
    {
        var registry = new HookRegistry<AfterCardDrawnHookContext>(AfterCardDrawn, "Draw-pile prediction");

        registry.Register<ConfusedPower>(HandleConfusedPower);
        registry.Register<Slither>(HandleSlither);
        registry.Register<IterationPower>(HandleIterationPower);
        registry.Register<PagestormPower>(HandlePagestormPower);
        registry.Register<AutomationPower>(SkipOriginal);
        registry.Register<ChainsOfBindingPower>(HandleChainsOfBindingPower);
        registry.Register<CorrosiveWavePower>(HandleDriftRiskIfOwner);
        registry.Register<SpeedsterPower>(HandleSpeedsterPower);
        registry.Register<KinglyKick>(HandleKinglyKick);
        registry.Register<KinglyPunch>(HandleKinglyPunch);
        registry.Register<Cards.Void>(SkipOriginal);

        return registry;
    }

    private static HookResultKind HandleHellraiserPower(HellraiserPower power, AfterCardDrawnHookContext context)
    {
        return context.PreviewCard.Owner.Creature == power.Owner && context.PreviewCard.Tags.Contains(CardTag.Strike)
            ? HookResultKind.DriftRisk
            : HookResultKind.Ignored;
    }

    private static HookResultKind HandleConfusedPower(ConfusedPower power, AfterCardDrawnHookContext context)
    {
        if (context.PreviewCard.Owner != power.Owner?.Player || context.PreviewCard.EnergyCost.Canonical < 0)
        {
            return HookResultKind.Ignored;
        }

        context.MutablePreviewCard.EnergyCost.SetThisCombat(context.EnergyCostRng.NextInt(4));
        return HookResultKind.Applied;
    }

    private static HookResultKind HandleSlither(Slither slither, AfterCardDrawnHookContext context)
    {
        if (context.OriginalCard != slither.Card)
        {
            return HookResultKind.Ignored;
        }

        context.MutablePreviewCard.EnergyCost.SetThisCombat(context.EnergyCostRng.NextInt(4));
        return HookResultKind.Applied;
    }

    private static HookResultKind HandleIterationPower(IterationPower power, AfterCardDrawnHookContext context)
    {
        if (context.PreviewCard.Owner.Creature != power.Owner || context.PreviewCard.Type != CardType.Status)
        {
            return HookResultKind.Ignored;
        }

        if (context.StatusCardsDrawnThisTurn.Value > 1)
        {
            return HookResultKind.Ignored;
        }

        context.Draw(power.Amount);
        return HookResultKind.Applied;
    }

    private static HookResultKind HandlePagestormPower(PagestormPower power, AfterCardDrawnHookContext context)
    {
        if (context.PreviewCard.Owner.Creature != power.Owner || !context.PreviewCard.Keywords.Contains(CardKeyword.Ethereal))
        {
            return HookResultKind.Ignored;
        }

        context.Draw(power.Amount);
        return HookResultKind.Applied;
    }

    private static HookResultKind HandleChainsOfBindingPower(ChainsOfBindingPower power, AfterCardDrawnHookContext context)
    {
        var bound = ModelDb.Affliction<Bound>();
        if (context.PreviewCard.Owner != power.Owner?.Player ||
            context.CombatState.CurrentSide != power.Owner.Side ||
            !bound.CanAfflict(context.PreviewCard) ||
            context.BoundCardsAfflictedThisTurn.Value >= power.Amount)
        {
            return HookResultKind.Ignored;
        }

        context.MutablePreviewCard.AfflictInternal(bound.ToMutable(), power.Amount);
        context.BoundCardsAfflictedThisTurn.Value++;
        return HookResultKind.Applied;
    }

    private static HookResultKind HandleSpeedsterPower(SpeedsterPower power, AfterCardDrawnHookContext context)
    {
        return !context.FromHandDraw &&
            context.PreviewCard.Owner.Creature == power.Owner &&
            context.PreviewCard.Owner.Creature.CombatState?.CurrentSide == context.PreviewCard.Owner.Creature.Side
                ? HookResultKind.DriftRisk
                : HookResultKind.Ignored;
    }

    private static HookResultKind HandleKinglyKick(KinglyKick card, AfterCardDrawnHookContext context)
    {
        if (context.OriginalCard != card)
        {
            return HookResultKind.Ignored;
        }

        context.MutablePreviewCard.EnergyCost.AddThisCombat(-1);
        return HookResultKind.Applied;
    }

    private static HookResultKind HandleKinglyPunch(KinglyPunch card, AfterCardDrawnHookContext context)
    {
        if (context.OriginalCard != card)
        {
            return HookResultKind.Ignored;
        }

        var previewCard = context.MutablePreviewCard;
        previewCard.DynamicVars.Damage.BaseValue += previewCard.DynamicVars["Increase"].BaseValue;
        return HookResultKind.Applied;
    }

    private static HookResultKind HandleDriftRiskIfOwner(PowerModel power, AfterCardDrawnHookContext context)
    {
        return context.PreviewCard.Owner.Creature == power.Owner
            ? HookResultKind.DriftRisk
            : HookResultKind.Ignored;
    }

    private static HookResultKind SkipOriginal(AbstractModel model, AfterCardDrawnHookContext context)
    {
        return HookResultKind.Ignored;
    }
}

internal sealed class AfterCardDrawnHookContext
{
    public required ICombatState CombatState { get; init; }

    public required Player Player { get; init; }

    public required PredictedCard Card { get; init; }

    public CardModel OriginalCard => Card.Original;

    public CardModel PreviewCard => Card.Preview;

    public CardModel MutablePreviewCard => Card.MutablePreview;

    public required bool FromHandDraw { get; init; }

    public required Rng EnergyCostRng { get; init; }

    public required StrongBox<int> StatusCardsDrawnThisTurn { get; init; }

    public required StrongBox<int> BoundCardsAfflictedThisTurn { get; init; }

    public required Action<int> Draw { get; init; }
}
