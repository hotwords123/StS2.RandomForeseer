using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.Common.Mirrors;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Hooks.TurnEnd;

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

    private static readonly Registry Registry = new(AfterFlush);

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
