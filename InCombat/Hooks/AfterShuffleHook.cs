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

    public static IReadOnlyList<HookResult> Run(AfterShuffleHookContext context)
    {
        return Registry.Run(context.CombatState.IterateHookListeners(), context);
    }

    private static HookRegistry<AfterShuffleHookContext> CreateRegistry()
    {
        var registry = new HookRegistry<AfterShuffleHookContext>(AfterShuffle);

        registry.Register<BiiigHug>(HandleBiiigHug);
        registry.Register<StratagemPower>(HandleStratagemPower);
        registry.Register<TheAbacus>(HandleTheAbacus);

        return registry;
    }

    private static HookResultKind HandleBiiigHug(BiiigHug relic, AfterShuffleHookContext context)
    {
        if (relic.Owner != context.Player)
        {
            return HookResultKind.Ignored;
        }

        var soot = PredictionUtils.CreateCard(ModelDb.Card<Soot>(), context.Player);
        var position = context.ShuffleRng.NextInt(context.DrawPileCards.Count + 1);
        context.DrawPileCards.Insert(position, new PredictedCard(soot));
        return HookResultKind.Applied;
    }

    private static HookResultKind HandleStratagemPower(StratagemPower power, AfterShuffleHookContext context)
    {
        return power.Owner?.Player == context.Player
            ? HookResultKind.DriftRisk
            : HookResultKind.Ignored;
    }

    private static HookResultKind HandleTheAbacus(TheAbacus relic, AfterShuffleHookContext context)
    {
        return relic.Owner == context.Player
            ? HookResultKind.DriftRisk
            : HookResultKind.Ignored;
    }
}

internal sealed class AfterShuffleHookContext
{
    public required ICombatState CombatState { get; init; }

    public required Player Player { get; init; }

    public required List<PredictedCard> DrawPileCards { get; init; }

    public required Rng ShuffleRng { get; init; }
}
