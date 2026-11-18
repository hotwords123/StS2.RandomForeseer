using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Nodes.Combat;
using RandomForeseer.Data;
using RandomForeseer.InCombat.Extensions;

namespace RandomForeseer.InCombat;

/// <summary>
/// Selects which prediction to show on each shared combat UI surface.
/// </summary>
/// <remarks>
/// <see cref="Set"/> always transfers ownership, including when the projection is <see langword="null"/>.
/// <see cref="Release"/> only clears projection owned by the supplied session, so stale cleanup cannot remove a
/// newer session's projection. End-turn results remain available as a fallback while an interaction is active.
/// </remarks>
internal static class CombatPredictionProjectionController
{
    private static object? _owner;
    private static CombatPredictionProjection? _actionProjection;
    private static CombatPredictionProjection? _endTurnProjection;
    private static NEndTurnButton? _focusedEndTurnButton;

    /// <summary>Transfers interaction ownership and publishes the supplied projection.</summary>
    public static void Set(object owner, CombatPredictionProjection? projection)
    {
        _owner = owner;
        SetActionProjection(projection);
    }

    /// <summary>Releases and clears projection only when <paramref name="owner"/> is the active owner.</summary>
    public static void Release(object owner)
    {
        if (_owner != owner)
        {
            return;
        }

        _owner = null;
        SetActionProjection(null);
    }

    public static void SetEndTurn(CombatPredictionProjection? projection)
    {
        if (ReferenceEquals(_endTurnProjection, projection))
        {
            return;
        }

        _endTurnProjection = projection;
        RefreshEndTurnDisplay();
    }

    /// <summary>Clears both prediction sources and their shared display at a combat lifecycle boundary.</summary>
    public static void ClearAll()
    {
        _owner = null;
        _actionProjection = null;
        _endTurnProjection = null;
        RefreshEndTurnDisplay();
    }

    public static void SetEndTurnButton(NEndTurnButton? focusedButton)
    {
        _focusedEndTurnButton = focusedButton;
        RefreshEndTurnDisplay();
    }

    public static void RefreshEndTurnDisplay()
    {
        RefreshCardHighlights();
        if (_actionProjection is not { DamagePrediction.HasTargets: true })
        {
            RefreshDamage();
        }

        if (_focusedEndTurnButton is { } button && _endTurnProjection?.HoverTips is { Count: > 0 } tips)
        {
            EndTurnButtonHoverTips.Show(button, tips);
        }
        else
        {
            EndTurnButtonHoverTips.Clear();
        }
    }

    private static void SetActionProjection(CombatPredictionProjection? projection)
    {
        var previous = _actionProjection;
        if (ReferenceEquals(previous, projection))
        {
            return;
        }

        _actionProjection = projection;
        RefreshCardHighlights();
        if (previous is { DamagePrediction.HasTargets: true } || projection is { DamagePrediction.HasTargets: true })
        {
            RefreshDamage();
        }
    }

    private static void RefreshCardHighlights()
    {
        var actionCards = _actionProjection?.HighlightedCards;
        var endTurnCards = ShouldShow(ModData.Settings.EndTurnCardHighlightDisplayMode)
            ? _endTurnProjection?.HighlightedCards
            : null;

        if (actionCards is not { Count: > 0 } && endTurnCards is not { Count: > 0 })
        {
            CombatPredictionCardHighlight.Show([]);
            return;
        }

        var handCards = LocalContext.GetMe(CombatManager.Instance.LiveCombatState)?.PlayerCombatState?.Hand.Cards;
        if (handCards is not { Count: > 0 })
        {
            CombatPredictionCardHighlight.Show([]);
            return;
        }

        var highlights = actionCards?.Where(handCards.Contains).ToArray();
        if (highlights is not { Length: > 0 })
        {
            highlights = endTurnCards?.Where(handCards.Contains).ToArray();
        }

        CombatPredictionCardHighlight.Show(highlights ?? []);
    }

    private static void RefreshDamage()
    {
        var action = _actionProjection is { DamagePrediction.HasTargets: true } ? _actionProjection : null;
        if ((action ?? _endTurnProjection) is not { DamagePrediction.HasTargets: true } projection)
        {
            EndTurnPredictionCreatureHoverTips.Clear();
            CombatPredictionOverlay.Clear();
            DamagePredictionHealthBarForecast.Clear();
            return;
        }

        var isEndTurn = action is null;
        var settings = ModData.Settings;

        if (isEndTurn)
        {
            EndTurnPredictionCreatureHoverTips.Set(projection.DamagePrediction, projection.Risk);
        }
        else
        {
            EndTurnPredictionCreatureHoverTips.Clear();
        }

        if (!isEndTurn || ShouldShow(settings.EndTurnDamageIndicatorDisplayMode))
        {
            CombatPredictionOverlay.Show(
                projection.DamagePrediction,
                projection.Risk,
                isEndTurn ? EndTurnPredictionCreatureHoverTips.GetHoverTips : null);
        }
        else
        {
            CombatPredictionOverlay.Clear();
        }

        if (!isEndTurn || ShouldShow(settings.EndTurnHealthBarForecastDisplayMode))
        {
            DamagePredictionHealthBarForecast.Set(projection.DamagePrediction);
        }
        else
        {
            DamagePredictionHealthBarForecast.Clear();
        }
    }

    private static bool ShouldShow(EndTurnPredictionDisplayMode displayMode)
    {
        return displayMode switch
        {
            EndTurnPredictionDisplayMode.ButtonHover => _focusedEndTurnButton != null,
            EndTurnPredictionDisplayMode.Always => true,
            _ => false
        };
    }
}
