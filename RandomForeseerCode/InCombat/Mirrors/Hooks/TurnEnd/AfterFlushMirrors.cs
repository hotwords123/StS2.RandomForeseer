using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using RandomForeseer.Common;
using RandomForeseer.Common.Mirrors;

namespace RandomForeseer.InCombat.Mirrors.Hooks.TurnEnd;

using Registry = MethodMirrorRegistry<AbstractModel, AfterFlushMirrorContext>;

/// <summary>Mirrors <see cref="AbstractModel.AfterFlush"/>.</summary>
internal static class AfterFlushMirrors
{
    private static readonly MirrorMethodSpec AfterFlush = MirrorMethodSpec.Hook(
        nameof(AbstractModel.AfterFlush),
        [
            typeof(PlayerChoiceContext),
            typeof(Player),
            typeof(IReadOnlyCollection<CardModel>),
            typeof(IReadOnlyCollection<CardModel>)
        ]);

    private static readonly Registry Registry = CreateRegistry();

    private static Registry CreateRegistry()
    {
        var registry = new Registry(AfterFlush);
        registry.Register<Bookmark>(HandleBookmark);
        return registry;
    }

    private static void HandleBookmark(Bookmark relic, AfterFlushMirrorContext context)
    {
        if (context.Player != relic.Owner)
        {
            return;
        }

        var cards = context.RetainedCards.Where(card =>
            !card.Preview.EnergyCost.CostsX &&
            card.Preview.EnergyCost.GetWithModifiers(CostModifiers.Local) > 0).ToList();
        if (cards.Count > 0)
        {
            var selected = context.Rng.CombatCardSelection.NextItem(cards);
            selected?.MutablePreview.EnergyCost.AddUntilPlayed(-1);
        }
    }

    public static void Invoke(AbstractModel listener, AfterFlushMirrorContext context)
    {
        Registry.Invoke(listener, context);
    }
}

internal sealed class AfterFlushMirrorContext : CombatMirrorContext
{
    public required Player Player { get; init; }

    public required IReadOnlyList<PredictedCard> FlushedCards { get; init; }

    public required IReadOnlyList<PredictedCard> RetainedCards { get; init; }
}
