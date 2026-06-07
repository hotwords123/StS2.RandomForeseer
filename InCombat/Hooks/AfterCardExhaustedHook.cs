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

    public static IReadOnlyList<HookResult> Run(AfterCardExhaustedHookContext context)
    {
        return Registry.Run(context.CombatState.IterateHookListeners(), context);
    }

    private static HookRegistry<AfterCardExhaustedHookContext> CreateRegistry()
    {
        var registry = new HookRegistry<AfterCardExhaustedHookContext>(AfterCardExhausted, "Exhaust prediction");

        registry.Register<BurningSticks>(HandleBurningSticks);
        registry.Register<CharonsAshes>(HandleRelicDriftRiskIfOwner);
        registry.Register<DarkEmbracePower>(HandleDarkEmbracePower);
        registry.Register<DrumOfBattle>(SkipOriginal);
        registry.Register<FeelNoPainPower>(HandlePowerDriftRiskIfOwner);
        registry.Register<ForgottenSoul>(HandleRelicDriftRiskIfOwner);
        registry.Register<JossPaper>(HandleJossPaper);
        registry.Register<SkillIronclad1Achievement>(SkipOriginal);

        return registry;
    }

    private static HookResultKind HandleBurningSticks(BurningSticks relic, AfterCardExhaustedHookContext context)
    {
        if (context.PreviewCard.Owner != relic.Owner ||
            context.BurningSticksUsedThisCombat.GetValueOrDefault(relic, relic.WasUsedThisCombat) ||
            context.PreviewCard.Type != CardType.Skill)
        {
            return HookResultKind.Ignored;
        }

        context.AddToHand(new PredictedCard(context.MutablePreviewCard.CreateClone()));
        context.BurningSticksUsedThisCombat[relic] = true;
        return HookResultKind.Applied;
    }

    private static HookResultKind HandleDarkEmbracePower(DarkEmbracePower power, AfterCardExhaustedHookContext context)
    {
        if (context.PreviewCard.Owner.Creature != power.Owner)
        {
            return HookResultKind.Ignored;
        }

        if (context.CausedByEthereal)
        {
            return HookResultKind.DriftRisk;
        }

        context.Draw(power.Amount);
        return HookResultKind.Applied;
    }

    private static HookResultKind HandleJossPaper(JossPaper relic, AfterCardExhaustedHookContext context)
    {
        if (context.PreviewCard.Owner != relic.Owner)
        {
            return HookResultKind.Ignored;
        }

        if (context.CausedByEthereal)
        {
            return HookResultKind.DriftRisk;
        }

        var cardsExhausted = context.JossPaperCardsExhausted.GetValueOrDefault(relic, relic.CardsExhausted) + 1;
        var threshold = relic.DynamicVars["ExhaustAmount"].IntValue;
        if (cardsExhausted >= threshold)
        {
            context.Draw(cardsExhausted / threshold);
            cardsExhausted %= threshold;
        }

        context.JossPaperCardsExhausted[relic] = cardsExhausted;
        return HookResultKind.Applied;
    }

    private static HookResultKind HandleRelicDriftRiskIfOwner(RelicModel relic, AfterCardExhaustedHookContext context)
    {
        return relic.Owner == context.PreviewCard.Owner
            ? HookResultKind.DriftRisk
            : HookResultKind.Ignored;
    }

    private static HookResultKind HandlePowerDriftRiskIfOwner(PowerModel power, AfterCardExhaustedHookContext context)
    {
        return power.Owner == context.PreviewCard.Owner.Creature
            ? HookResultKind.DriftRisk
            : HookResultKind.Ignored;
    }

    private static HookResultKind SkipOriginal(AbstractModel model, AfterCardExhaustedHookContext context)
    {
        return HookResultKind.Ignored;
    }
}

internal sealed class AfterCardExhaustedHookContext
{
    public required ICombatState CombatState { get; init; }

    public required Player Player { get; init; }

    public required PredictedCard Card { get; init; }

    public CardModel OriginalCard => Card.Original;

    public CardModel PreviewCard => Card.Preview;

    public CardModel MutablePreviewCard => Card.MutablePreview;

    public required bool CausedByEthereal { get; init; }

    public required Dictionary<RelicModel, int> JossPaperCardsExhausted { get; init; }

    public required Dictionary<RelicModel, bool> BurningSticksUsedThisCombat { get; init; }

    public required Action<int> Draw { get; init; }

    public required Action<PredictedCard> AddToHand { get; init; }
}
