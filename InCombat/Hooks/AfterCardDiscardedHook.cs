using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using RandomForeseer.Common.Hooks;

namespace RandomForeseer.InCombat.Hooks;

// Mirrors the prediction-relevant parts of Hook.AfterCardDiscarded.
internal static class AfterCardDiscardedHook
{
    private static readonly HookSpec AfterCardDiscarded = new(
        nameof(AbstractModel.AfterCardDiscarded),
        [
            typeof(PlayerChoiceContext),
            typeof(CardModel)
        ]);

    private static readonly HookRegistry<AfterCardDiscardedHookContext> Registry = CreateRegistry();

    public static void Run(AfterCardDiscardedHookContext context)
    {
        Registry.Run(context.CombatState.IterateHookListeners(), context);
    }

    private static HookRegistry<AfterCardDiscardedHookContext> CreateRegistry()
    {
        var registry = new HookRegistry<AfterCardDiscardedHookContext>(AfterCardDiscarded);

        registry.Register<Tingsha>(HandleTingsha);
        registry.Register<ToughBandages>(HandleToughBandages);

        return registry;
    }

    private static void HandleTingsha(Tingsha relic, AfterCardDiscardedHookContext context)
    {
        if (relic.Owner != context.PreviewCard.Owner ||
            relic.Owner.Creature.Side != context.CombatState.CurrentSide)
        {
            return;
        }

        var target = context.Rng.CombatTargets.NextItem(
            context.State.GetHittableOpponentsOf(relic.Owner.Creature));
        if (target != null)
        {
            context.Simulator.Damage(
                [target],
                relic.DynamicVars.Damage.BaseValue,
                relic.DynamicVars.Damage.Props,
                relic.Owner.Creature);
        }
    }

    private static void HandleToughBandages(ToughBandages relic, AfterCardDiscardedHookContext context)
    {
        if (relic.Owner != context.PreviewCard.Owner ||
            relic.Owner.Creature.Side != context.CombatState.CurrentSide)
        {
            return;
        }

        context.Simulator.GainBlock(
            relic.Owner.Creature,
            relic.DynamicVars.Block.BaseValue,
            relic.DynamicVars.Block.Props);
    }
}

internal sealed class AfterCardDiscardedHookContext : CombatCardPredictionHookContext
{
}
