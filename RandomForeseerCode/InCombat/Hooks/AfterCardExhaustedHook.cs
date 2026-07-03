using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Achievements;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.ValueProps;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.Common.Hooks;

namespace RandomForeseer.RandomForeseerCode.InCombat.Hooks;

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
        registry.Register<CharonsAshes>(HandleCharonsAshes);
        registry.Register<DarkEmbracePower>(HandleDarkEmbracePower);
        registry.RegisterIgnored<DrumOfBattle>();
        registry.Register<FeelNoPainPower>(HandleFeelNoPainPower);
        registry.Register<ForgottenSoul>(HandleForgottenSoul);
        registry.Register<JossPaper>(HandleJossPaper);
        registry.Register<Midnight>(HandleMidnight);
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

        var copy = (CardModel)context.PreviewCard.MutableClone();
        context.Simulator.AddToHand(context.Player, PredictedCard.FromGenerated(copy));
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
            // Ethereal exhaust only records the count here in vanilla; the actual draw happens
            // later in end-turn cleanup, which this simulation path does not include.
            return;
        }

        context.Simulator.Draw(context.Player, power.Amount);
    }

    private static void HandleJossPaper(JossPaper relic, AfterCardExhaustedHookContext context)
    {
        if (context.PreviewCard.Owner != relic.Owner)
        {
            return;
        }

        if (context.CausedByEthereal)
        {
            // Ethereal exhaust only records the count here in vanilla; the actual draw happens
            // later in end-turn cleanup, which this simulation path does not include.
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
            context.Simulator.Draw(context.Player, cardsExhausted / threshold);
            cardsExhausted %= threshold;
        }

        state.CardsExhausted = cardsExhausted;
    }

    private static void HandleCharonsAshes(CharonsAshes relic, AfterCardExhaustedHookContext context)
    {
        if (relic.Owner != context.PreviewCard.Owner)
        {
            return;
        }

        context.Simulator.Damage(
            context.State.GetHittableOpponentsOf(relic.Owner.Creature),
            relic.DynamicVars.Damage,
            relic.Owner.Creature);
    }

    private static void HandleForgottenSoul(ForgottenSoul relic, AfterCardExhaustedHookContext context)
    {
        if (relic.Owner != context.PreviewCard.Owner)
        {
            return;
        }

        var target = context.Rng.CombatTargets.NextItem(
            context.State.GetHittableOpponentsOf(relic.Owner.Creature));
        if (target != null)
        {
            context.Simulator.Damage(target, relic.DynamicVars.Damage, relic.Owner.Creature);
        }
    }

    private static void HandleFeelNoPainPower(FeelNoPainPower power, AfterCardExhaustedHookContext context)
    {
        if (power.Owner != context.PreviewCard.Owner.Creature)
        {
            return;
        }

        context.Simulator.GainBlock(power.Owner, power.Amount, ValueProp.Unpowered);
    }

    private static void HandleMidnight(Midnight card, AfterCardExhaustedHookContext context)
    {
        // StS2 v0.108.0 added Midnight's global exhaust listener; mutate only the predicted instance.
        if (context.State.GetPlayerCombatState(card.Owner).FindCard(card) is { } midnight)
        {
            midnight.MutablePreview.EnergyCost.AddThisCombat(-1);
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

internal sealed class AfterCardExhaustedHookContext : CombatPredictionCardHookContext
{
    public required Player Player { get; init; }

    public required bool CausedByEthereal { get; init; }

}
