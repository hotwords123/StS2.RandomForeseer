using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Random;
using RandomForeseer.Common;
using RandomForeseer.Common.Hooks;

namespace RandomForeseer.InCombat.Hooks;

// Mirrors the prediction-relevant parts of the original Hook.AfterShuffle chain:
// Hook.AfterShuffle iterates combat hook listeners and calls AbstractModel.AfterShuffle.
internal static class AfterShuffleHook
{
    private static readonly HookSpec AfterShuffle = new(
        nameof(AbstractModel.AfterShuffle),
        [
            typeof(PlayerChoiceContext),
            typeof(Player)
        ]);

    private static readonly HookRegistry<AfterShuffleHookContext> Registry = CreateRegistry();

    public static void Run(AfterShuffleHookContext context)
    {
        Registry.Run(context.CombatState.IterateHookListeners(), context);
    }

    private static HookRegistry<AfterShuffleHookContext> CreateRegistry()
    {
        var registry = new HookRegistry<AfterShuffleHookContext>(AfterShuffle);

        registry.Register<BiiigHug>(HandleBiiigHug);
        registry.Register<StratagemPower>(HandleStratagemPower);
        registry.Register<TheAbacus>(HandleTheAbacus);

        return registry;
    }

    private static void HandleBiiigHug(BiiigHug relic, AfterShuffleHookContext context)
    {
        if (relic.Owner != context.Player)
        {
            return;
        }

        var soot = PredictionUtils.CreateCard(ModelDb.Card<Soot>(), context.Player);
        var position = context.ShuffleRng.NextInt(context.DrawPileCards.Count + 1);
        context.DrawPileCards.Insert(position, new PredictedCard(soot));
    }

    private static void HandleStratagemPower(StratagemPower power, AfterShuffleHookContext context)
    {
        // Stratagem opens a combat-pile card selection and moves chosen cards to hand;
        // combat pile selection is not mirrored here.
        if (power.Owner?.Player == context.Player)
        {
            context.RiskTracker.AddCurrentSource();
        }
    }

    private static void HandleTheAbacus(TheAbacus relic, AfterShuffleHookContext context)
    {
        if (relic.Owner != context.Player)
        {
            return;
        }

        context.Executor.GainBlock(
            relic.Owner.Creature,
            relic.DynamicVars.Block.BaseValue,
            relic.DynamicVars.Block.Props);
    }
}

internal sealed class AfterShuffleHookContext : IPredictionHookContext
{
    public required PredictionRiskTracker RiskTracker { get; init; }

    public required IDamageBlockExecutor Executor { get; init; }

    public required ICombatState CombatState { get; init; }

    public required Player Player { get; init; }

    public required List<PredictedCard> DrawPileCards { get; init; }

    public required Rng ShuffleRng { get; init; }
}
