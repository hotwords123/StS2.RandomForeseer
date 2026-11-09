using RandomForeseer.Common;

namespace RandomForeseer.InCombat;

/// <summary>
/// Owns the global UI surfaces shared by combat card, potion, and end-turn predictions.
/// </summary>
/// <remarks>
/// <see cref="Set"/> always transfers ownership, including when the projection is <see langword="null"/>.
/// <see cref="Release"/> only clears projection owned by the supplied session, so stale cleanup cannot remove a
/// newer session's projection. Source-specific HoverTips remain the responsibility of card and potion adapters.
/// </remarks>
internal static class CombatPredictionProjectionController
{
    private static object? _owner;
    private static bool _hasDamagePrediction;

    /// <summary>Transfers projection ownership and applies or clears the supplied projection.</summary>
    public static void Set(object owner, CombatPredictionProjection? projection)
    {
        _owner = owner;

        if (projection is not null)
        {
            ShowDamagePrediction(projection.DamagePrediction, projection.Risk);
            CombatPredictionCardHighlight.Show(projection.HighlightedCards);
        }
        else
        {
            ClearDamagePrediction();
            CombatPredictionCardHighlight.Clear();
        }

        EndTurnPredictionController.SetActionDamageOverride(_hasDamagePrediction);
    }

    /// <summary>Releases and clears projection only when <paramref name="owner"/> is the active owner.</summary>
    public static void Release(object owner)
    {
        if (_owner != owner)
        {
            return;
        }

        _owner = null;
        ClearDamagePrediction();
        CombatPredictionCardHighlight.Clear();
        EndTurnPredictionController.SetActionDamageOverride(false);
    }

    private static void ShowDamagePrediction(DamagePrediction damagePrediction, PredictionRisk risk)
    {
        if (!damagePrediction.HasTargets)
        {
            ClearDamagePrediction();
            return;
        }

        CombatPredictionOverlay.Show(damagePrediction, risk);
        DamagePredictionHealthBarForecast.Set(damagePrediction);
        _hasDamagePrediction = true;
    }

    private static void ClearDamagePrediction()
    {
        if (!_hasDamagePrediction)
        {
            return;
        }

        CombatPredictionOverlay.Clear();
        DamagePredictionHealthBarForecast.Clear();
        _hasDamagePrediction = false;
    }
}
