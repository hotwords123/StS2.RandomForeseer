

using MegaCrit.Sts2.Core.Models;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Cards.OnPlay;
using RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Potions.OnUse;

namespace RandomForeseer.RandomForeseerCode.InCombat.Extensions;

/// <summary>
/// Provides combat-projection queries over the shared immutable prediction trace.
/// </summary>
internal static class CombatPredictionTraceFrameExtensions
{
    /// <summary>
    /// Finds the nearest card/potion effect boundary responsible for the current frame.
    /// </summary>
    /// <remarks>
    /// A boundary is either a mirrored <see cref="CardModel.OnPlay"/>/<see cref="PotionModel.OnUse"/> invocation or
    /// its enclosing <see cref="PredictionActionKind.CardPlay"/>/<see cref="PredictionActionKind.PotionUse"/> action.
    /// Action frames are checked in the same nearest-to-farthest walk as method frames, so a nested action is retained
    /// even when its model method is itself reached from an enclosing <c>OnPlay</c> invocation.
    /// </remarks>
    /// <returns>
    /// The nearest effect or action frame, or <see langword="null"/> when the trace has no such boundary.
    /// </returns>
    public static PredictionTraceFrame? FindOriginatingEffect(this PredictionTraceFrame trace)
    {
        return trace.Ancestors()
            .FirstOrDefault(static frame =>
                CardOnPlayMirrors.IsOnPlayInvocation(frame.Invocation) ||
                PotionOnUseMirrors.IsOnUseInvocation(frame.Invocation) ||
                frame.Invocation.Action is PredictionActionKind.CardPlay or PredictionActionKind.PotionUse);
    }

    /// <summary>
    /// Finds the nearest card-play or potion-use action responsible for the current frame.
    /// </summary>
    /// <returns>
    /// The nearest card-play or potion-use action frame, or <see langword="null"/> when the trace has no such action.
    /// </returns>
    public static PredictionTraceFrame? FindOriginatingAction(this PredictionTraceFrame trace)
    {
        return trace.Ancestors()
            .FirstOrDefault(static frame => frame.Invocation.Action is
                PredictionActionKind.CardPlay or
                PredictionActionKind.PotionUse);
    }
}
