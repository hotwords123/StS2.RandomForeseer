using System.Runtime.CompilerServices;
using Godot;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes.HoverTips;

namespace RandomForeseer.RandomForeseerCode.Common.HoverTips;

/// <summary>
/// Creates, tracks, and removes HoverTip sets explicitly owned by prediction-only UI surfaces.
/// </summary>
/// <remarks>
/// This helper never replaces an already active vanilla or prediction HoverTip set. Callers that receive a non-null
/// set from <see cref="EnsureHoverTipSet"/> must later call <see cref="RemoveOwnedHoverTipSet"/> for the same owner.
/// </remarks>
internal static class PredictionHoverTipSetHelper
{
    private static readonly ConditionalWeakTable<Control, NHoverTipSet> OwnedHoverTips = [];

    /// <summary>
    /// Creates an empty-owned HoverTip set, allowing the global prediction injection patch to populate it.
    /// </summary>
    /// <remarks>
    /// Returns <see langword="null"/> when the owner already has an active set or vanilla creation fails; it does not
    /// return an existing set. Successful sets are recorded for ownership-aware cleanup.
    /// </remarks>
    public static NHoverTipSet? EnsureHoverTipSet(Control owner, HoverTipAlignment alignment = HoverTipAlignment.None)
    {
        if (NHoverTipSet._activeHoverTips.ContainsKey(owner))
        {
            return null;
        }

        var tipSet = NHoverTipSet.CreateAndShow(owner, [], alignment);
        if (tipSet == null)
        {
            return null;
        }

        OwnedHoverTips.AddOrUpdate(owner, tipSet);
        return tipSet;
    }

    /// <summary>
    /// Removes the prediction-owned set for an owner when it is still the active set.
    /// </summary>
    /// <remarks>
    /// This is safe to call repeatedly and never removes a newer or independently created HoverTip set.
    /// </remarks>
    public static void RemoveOwnedHoverTipSet(Control owner)
    {
        if (!OwnedHoverTips.TryGetValue(owner, out var tipSet))
        {
            return;
        }

        OwnedHoverTips.Remove(owner);

        if (NHoverTipSet._activeHoverTips.TryGetValue(owner, out var activeTipSet) &&
            ReferenceEquals(activeTipSet, tipSet))
        {
            NHoverTipSet.Remove(owner);
        }
    }
}
