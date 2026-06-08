using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using RandomForeseer.Common;
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

    public static IReadOnlyList<HookResult> Run(AfterCardDiscardedHookContext context)
    {
        return Registry.Run(context.CombatState.IterateHookListeners(), context);
    }

    private static HookRegistry<AfterCardDiscardedHookContext> CreateRegistry()
    {
        var registry = new HookRegistry<AfterCardDiscardedHookContext>(AfterCardDiscarded, "Discard prediction");

        registry.Register<Tingsha>(HandleRelicDriftRiskIfOwner);
        registry.Register<ToughBandages>(HandleRelicDriftRiskIfOwner);

        return registry;
    }

    private static HookResultKind HandleRelicDriftRiskIfOwner(
        RelicModel relic,
        AfterCardDiscardedHookContext context)
    {
        return relic.Owner == context.PreviewCard.Owner &&
            relic.Owner.Creature.Side == context.CombatState.CurrentSide
            ? HookResultKind.DriftRisk
            : HookResultKind.Ignored;
    }
}

internal sealed class AfterCardDiscardedHookContext
{
    public required ICombatState CombatState { get; init; }

    public required PredictedCard Card { get; init; }

    public CardModel OriginalCard => Card.Original;

    public CardModel PreviewCard => Card.Preview;
}
