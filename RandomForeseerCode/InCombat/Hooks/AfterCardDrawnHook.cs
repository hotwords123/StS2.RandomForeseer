using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Afflictions;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using RandomForeseer.RandomForeseerCode.Common.Hooks;
using Cards = MegaCrit.Sts2.Core.Models.Cards;

namespace RandomForeseer.RandomForeseerCode.InCombat.Hooks;

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
        if (context.PreviewCard.Owner.Creature == power.Owner &&
            context.PreviewCard.Tags.Contains(CardTag.Strike))
        {
            context.MarkCurrentSourceRisky();
        }
    }

    private static void HandleConfusedPower(ConfusedPower power, AfterCardDrawnHookContext context)
    {
        if (context.PreviewCard.Owner != power.Owner?.Player ||
            context.PreviewCard.EnergyCost.Canonical < 0)
        {
            return;
        }

        context.MutablePreviewCard.EnergyCost.SetThisCombat(context.Rng.CombatEnergyCosts.NextInt(4));
    }

    private static void HandleSlither(Slither slither, AfterCardDrawnHookContext context)
    {
        if (context.OriginalCard != slither.Card)
        {
            return;
        }

        context.MutablePreviewCard.EnergyCost.SetThisCombat(context.Rng.CombatEnergyCosts.NextInt(4));
    }

    private static void HandleIterationPower(IterationPower power, AfterCardDrawnHookContext context)
    {
        var drawState = context.State.GetPlayerCombatState(context.Player).CardDrawState;
        if (context.PreviewCard.Owner.Creature != power.Owner ||
            context.PreviewCard.Type != CardType.Status ||
            drawState.StatusCardsDrawnThisTurn > 1)
        {
            return;
        }

        context.Simulator.Draw(context.Player, power.Amount);
    }

    private static void HandlePagestormPower(PagestormPower power, AfterCardDrawnHookContext context)
    {
        if (context.PreviewCard.Owner.Creature != power.Owner ||
            !context.PreviewCard.Keywords.Contains(CardKeyword.Ethereal))
        {
            return;
        }

        context.Simulator.Draw(context.Player, power.Amount);
    }

    private static void HandleChainsOfBindingPower(ChainsOfBindingPower power, AfterCardDrawnHookContext context)
    {
        var drawState = context.State.GetPlayerCombatState(context.Player).CardDrawState;
        var bound = ModelDb.Affliction<Bound>();
        if (context.PreviewCard.Owner != power.Owner?.Player ||
            context.CombatState.CurrentSide != power.Owner.Side ||
            !bound.CanAfflict(context.PreviewCard) ||
            drawState.BoundCardsAfflictedThisTurn >= power.Amount)
        {
            return;
        }

        context.MutablePreviewCard.AfflictInternal(bound.ToMutable(), power.Amount);
        drawState.BoundCardsAfflictedThisTurn++;
    }

    private static void HandleSpeedsterPower(SpeedsterPower power, AfterCardDrawnHookContext context)
    {
        if (context.FromHandDraw ||
            context.PreviewCard.Owner.Creature != power.Owner ||
            context.PreviewCard.Owner.Creature.Side != context.CombatState.CurrentSide)
        {
            return;
        }

        context.Simulator.Damage(
            context.State.GetHittableOpponentsOf(power.Owner),
            power.Amount,
            ValueProp.Unpowered,
            power.Owner);
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

        var previewCard = (KinglyPunch)context.MutablePreviewCard;
        var damageIncrease = previewCard.DynamicVars["Increase"].BaseValue;
        previewCard.DynamicVars.Damage.BaseValue += damageIncrease;
        previewCard.ExtraDamage += damageIncrease;
    }

    private static void HandleDriftRiskIfOwner(PowerModel power, AfterCardDrawnHookContext context)
    {
        // CorrosiveWavePower applies Poison to all hittable enemies; power application
        // side effects are not mirrored here.
        if (context.PreviewCard.Owner.Creature == power.Owner)
        {
            context.MarkCurrentSourceRisky();
        }
    }

}

internal sealed class AfterCardDrawnHookContext : CombatPredictionCardHookContext
{
    public required Player Player { get; init; }

    public required bool FromHandDraw { get; init; }

}
