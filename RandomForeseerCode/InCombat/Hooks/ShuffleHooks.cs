using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.Common.Hooks;

namespace RandomForeseer.RandomForeseerCode.InCombat.Hooks;

internal static class ShuffleHooks
{
    private static readonly HookSpec ModifyShuffleOrder = new(
        nameof(AbstractModel.ModifyShuffleOrder),
        [
            typeof(Player),
            typeof(List<CardModel>),
            typeof(bool)
        ]);

    private static readonly HookSpec AfterShuffle = new(
        nameof(AbstractModel.AfterShuffle),
        [
            typeof(PlayerChoiceContext),
            typeof(Player)
        ]);

    private static readonly HookRegistry<ModifyShuffleOrderHookContext> ModifyShuffleOrderRegistry =
        CreateModifyShuffleOrderRegistry();

    private static readonly HookRegistry<AfterShuffleHookContext> AfterShuffleRegistry =
        CreateAfterShuffleRegistry();

    public static void RunModifyShuffleOrder(ModifyShuffleOrderHookContext context)
    {
        ModifyShuffleOrderRegistry.Run(context.CombatState.IterateHookListeners(), context);
    }

    public static void RunAfterShuffle(AfterShuffleHookContext context)
    {
        AfterShuffleRegistry.Run(context.CombatState.IterateHookListeners(), context);
    }

    private static HookRegistry<ModifyShuffleOrderHookContext> CreateModifyShuffleOrderRegistry()
    {
        var registry = new HookRegistry<ModifyShuffleOrderHookContext>(ModifyShuffleOrder);

        registry.Register<PerfectFit>(HandlePerfectFit);

        return registry;
    }

    private static HookRegistry<AfterShuffleHookContext> CreateAfterShuffleRegistry()
    {
        var registry = new HookRegistry<AfterShuffleHookContext>(AfterShuffle);

        registry.Register<BiiigHug>(HandleBiiigHug);
        registry.Register<StratagemPower>(HandleStratagemPower);
        registry.Register<TheAbacus>(HandleTheAbacus);

        return registry;
    }

    private static void HandlePerfectFit(PerfectFit enchantment, ModifyShuffleOrderHookContext context)
    {
        if (context.IsInitialShuffle ||
            context.Player != enchantment.Card.Owner ||
            context.DrawPileCards.FirstOrDefault(card => card.Original == enchantment.Card) is not { } card)
        {
            return;
        }

        context.DrawPileCards.Remove(card);
        context.DrawPileCards.Insert(0, card);
    }

    private static void HandleBiiigHug(BiiigHug relic, AfterShuffleHookContext context)
    {
        if (relic.Owner != context.Player)
        {
            return;
        }

        var soot = PredictionUtils.CreateCard(ModelDb.Card<Soot>(), context.Player);
        var position = context.Rng.Shuffle.NextInt(context.DrawPileCards.Count + 1);
        context.DrawPileCards.Insert(position, PredictedCard.FromGenerated(soot));
    }

    private static void HandleStratagemPower(StratagemPower power, AfterShuffleHookContext context)
    {
        // Stratagem opens a combat-pile card selection and moves chosen cards to hand;
        // combat pile selection is not mirrored here.
        if (power.Owner?.Player == context.Player)
        {
            context.MarkCurrentSourceRisky();
        }
    }

    private static void HandleTheAbacus(TheAbacus relic, AfterShuffleHookContext context)
    {
        if (relic.Owner != context.Player)
        {
            return;
        }

        context.Simulator.GainBlock(relic.Owner.Creature, relic.DynamicVars.Block);
    }
}

internal sealed class ModifyShuffleOrderHookContext : CombatPredictionHookContext
{
    public required Player Player { get; init; }

    public required List<PredictedCard> DrawPileCards { get; init; }

    public required bool IsInitialShuffle { get; init; }
}

internal sealed class AfterShuffleHookContext : CombatPredictionHookContext
{
    public required Player Player { get; init; }

    public required List<PredictedCard> DrawPileCards { get; init; }

}
