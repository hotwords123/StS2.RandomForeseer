using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Achievements;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using RandomForeseer.Common;
using RandomForeseer.Common.Hooks;

namespace RandomForeseer.InCombat.Hooks;

// Mirrors the prediction-relevant parts of Hook.AfterCardExhausted.
internal static class AfterCardExhaustedHook
{
    private static readonly HookSpec AfterCardExhausted = new(
        nameof(AbstractModel.AfterCardExhausted),
        [
            typeof(PlayerChoiceContext),
            typeof(CardModel),
            typeof(bool)
        ]);

    private static readonly HookRegistry<AfterCardExhaustedHookContext> Registry = CreateRegistry();

    public static void Run(AfterCardExhaustedHookContext context)
    {
        Registry.Run(context.CombatState.IterateHookListeners(), context);
    }

    private static HookRegistry<AfterCardExhaustedHookContext> CreateRegistry()
    {
        var registry = new HookRegistry<AfterCardExhaustedHookContext>(AfterCardExhausted);

        registry.Register<BurningSticks>(HandleBurningSticks);
        registry.Register<CharonsAshes>(HandleRelicDriftRiskIfOwner);
        registry.Register<DarkEmbracePower>(HandleDarkEmbracePower);
        registry.RegisterIgnored<DrumOfBattle>();
        registry.Register<FeelNoPainPower>(HandlePowerDriftRiskIfOwner);
        registry.Register<ForgottenSoul>(HandleRelicDriftRiskIfOwner);
        registry.Register<JossPaper>(HandleJossPaper);
        registry.RegisterIgnored<SkillIronclad1Achievement>();

        return registry;
    }

    private static void HandleBurningSticks(BurningSticks relic, AfterCardExhaustedHookContext context)
    {
        if (context.PreviewCard.Owner != relic.Owner ||
            context.PreviewCard.Type != CardType.Skill)
        {
            return;
        }

        var state = context.StateStore.Get(relic, () => new BurningSticksPredictionState
        {
            WasUsedThisCombat = relic.WasUsedThisCombat
        });

        if (state.WasUsedThisCombat)
        {
            return;
        }

        context.AddToHand(new PredictedCard(context.MutablePreviewCard.CreateClone()));
        state.WasUsedThisCombat = true;
    }

    private static void HandleDarkEmbracePower(DarkEmbracePower power, AfterCardExhaustedHookContext context)
    {
        if (context.PreviewCard.Owner.Creature != power.Owner)
        {
            return;
        }

        if (context.CausedByEthereal)
        {
            // Deferred: ethereal exhaust happens during end-turn cleanup, and this prediction
            // currently only mirrors explicit draw/exhaust flows.
            context.RiskTracker.AddCurrentSource();
            return;
        }

        context.Draw(power.Amount);
    }

    private static void HandleJossPaper(JossPaper relic, AfterCardExhaustedHookContext context)
    {
        if (context.PreviewCard.Owner != relic.Owner)
        {
            return;
        }

        if (context.CausedByEthereal)
        {
            // Deferred: ethereal exhaust happens during end-turn cleanup, and this prediction
            // currently only mirrors explicit draw/exhaust flows.
            context.RiskTracker.AddCurrentSource();
            return;
        }

        var state = context.StateStore.Get(relic, () => new JossPaperPredictionState
        {
            CardsExhausted = relic.CardsExhausted
        });

        var cardsExhausted = state.CardsExhausted + 1;
        var threshold = relic.DynamicVars["ExhaustAmount"].IntValue;
        if (cardsExhausted >= threshold)
        {
            context.Draw(cardsExhausted / threshold);
            cardsExhausted %= threshold;
        }

        state.CardsExhausted = cardsExhausted;
    }

    private static void HandleRelicDriftRiskIfOwner(RelicModel relic, AfterCardExhaustedHookContext context)
    {
        // Deferred:
        // - CharonsAshes is all-enemy damage and waits for a real damage detector path.
        // - ForgottenSoul chooses a random damage target through CombatTargets.NextItem.
        if (relic.Owner == context.PreviewCard.Owner)
        {
            context.RiskTracker.AddCurrentSource();
        }
    }

    private static void HandlePowerDriftRiskIfOwner(PowerModel power, AfterCardExhaustedHookContext context)
    {
        // Deferred for FeelNoPainPower: pure block, but DrawPilePrediction first needs to
        // share a DamageBlockRiskDetector session with these hooks.
        if (power.Owner == context.PreviewCard.Owner.Creature)
        {
            context.RiskTracker.AddCurrentSource();
        }
    }

}

internal sealed class BurningSticksPredictionState
{
    public bool WasUsedThisCombat { get; set; }
}

internal sealed class JossPaperPredictionState
{
    public int CardsExhausted { get; set; }
}

internal sealed class AfterCardExhaustedHookContext : IPredictionHookContext
{
    public required PredictionRiskTracker RiskTracker { get; init; }

    public required ICombatState CombatState { get; init; }

    public required Player Player { get; init; }

    public required PredictedCard Card { get; init; }

    public CardModel OriginalCard => Card.Original;

    public CardModel PreviewCard => Card.Preview;

    public CardModel MutablePreviewCard => Card.MutablePreview;

    public required bool CausedByEthereal { get; init; }

    public required PredictionStateStore StateStore { get; init; }

    public required Action<int> Draw { get; init; }

    public required Action<PredictedCard> AddToHand { get; init; }
}
