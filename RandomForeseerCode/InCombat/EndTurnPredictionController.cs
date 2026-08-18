using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using RandomForeseer.RandomForeseerCode.Common.HoverTips;
using RandomForeseer.RandomForeseerCode.Data;
using RandomForeseer.RandomForeseerCode.Settings;
using RandomForeseer.RandomForeseerCode.Telemetry;
using STS2RitsuLib.Settings;

namespace RandomForeseer.RandomForeseerCode.InCombat;

internal static class EndTurnPredictionController
{
    private static bool _isSubscribed;
    private static bool _isActionDamageOverrideActive;
    private static bool _isDeferredRefreshScheduled;
    private static bool _refreshPending;
    private static NEndTurnButton? _focusedEndTurnButton;

    public static void Subscribe()
    {
        if (_isSubscribed)
        {
            return;
        }

        CombatManager.Instance.AboutToSwitchToEnemyTurn += OnAboutToSwitchToEnemyTurn;
        CombatManager.Instance.PlayerEndedTurn += OnPlayerEndedTurn;
        CombatManager.Instance.PlayerUnendedTurn += OnPlayerUnendedTurn;
        CombatManager.Instance.CombatEnded += OnCombatEnded;
        CombatManager.Instance.StateTracker.CombatStateChanged += OnCombatStateChanged;
        RunManager.Instance.ActionExecutor.AfterActionExecuted += OnActionExecuted;
        ModSettingsBindingWriteEvents.ValueWritten += OnSettingsValueWritten;

        _isSubscribed = true;
        Refresh();
    }

    public static void Unsubscribe()
    {
        if (!_isSubscribed)
        {
            return;
        }

        CombatManager.Instance.AboutToSwitchToEnemyTurn -= OnAboutToSwitchToEnemyTurn;
        CombatManager.Instance.PlayerEndedTurn -= OnPlayerEndedTurn;
        CombatManager.Instance.PlayerUnendedTurn -= OnPlayerUnendedTurn;
        CombatManager.Instance.CombatEnded -= OnCombatEnded;
        CombatManager.Instance.StateTracker.CombatStateChanged -= OnCombatStateChanged;
        RunManager.Instance.ActionExecutor.AfterActionExecuted -= OnActionExecuted;
        ModSettingsBindingWriteEvents.ValueWritten -= OnSettingsValueWritten;

        _isSubscribed = false;
        Cleanup();
    }

    public static void OnEndTurnButtonFocused(NEndTurnButton endTurnButton)
    {
        _focusedEndTurnButton = endTurnButton;
        Refresh();
    }

    public static void OnEndTurnButtonUnfocused()
    {
        _focusedEndTurnButton = null;
        Refresh();
    }

    public static void Refresh()
    {
        if (_isActionDamageOverrideActive)
        {
            EndTurnPredictionCreatureHoverTips.Clear();
            EndTurnButtonHoverTipHelper.HideHoverTips();
            return;
        }

        if (NCombatRoom.Instance == null || !EndTurnPrediction.ShouldPredict())
        {
            Cleanup();
            return;
        }

        if (IsRefreshDeferred())
        {
            _refreshPending = true;
            return;
        }

        _refreshPending = false;

        EndTurnPredictionResult? prediction;
        try
        {
            prediction = EndTurnPrediction.Predict();
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"End-turn prediction refresh failed: {ex}");
            ModTelemetry.CaptureException(ex, "end_turn_prediction", "refresh");
            prediction = null;
        }

        if (prediction is not { DamagePrediction.HasTargets: true })
        {
            Clear();
            return;
        }

        EndTurnPredictionCreatureHoverTips.Set(prediction.DamagePrediction, prediction.Risk);

        var settings = ModData.Settings;
        if (ShouldShow(settings.EndTurnPredictionDisplayMode))
        {
            CombatPredictionOverlay.Show(
                prediction.DamagePrediction,
                prediction.Risk,
                EndTurnPredictionCreatureHoverTips.GetHoverTips);
        }
        else
        {
            CombatPredictionOverlay.Clear();
        }

        if (ShouldShow(settings.EndTurnHealthBarForecastDisplayMode))
        {
            DamagePredictionHealthBarForecast.Set(prediction.DamagePrediction);
        }
        else
        {
            DamagePredictionHealthBarForecast.Clear();
        }

        if (settings.EndTurnPredictionDisplayMode is EndTurnPredictionDisplayMode.EndTurnButtonHover &&
            _focusedEndTurnButton != null)
        {
            EndTurnButtonHoverTipHelper.ShowHoverTips(_focusedEndTurnButton,
            [
                PredictionHoverTipFactory.Text("end_turn_prediction_indicator"),
                .. prediction.Risk.ToHoverTips()
            ]);
        }
        else
        {
            EndTurnButtonHoverTipHelper.HideHoverTips();
        }
    }

    public static void Clear()
    {
        _refreshPending = false;
        EndTurnPredictionCreatureHoverTips.Clear();
        CombatPredictionOverlay.Clear();
        DamagePredictionHealthBarForecast.Clear();
        EndTurnButtonHoverTipHelper.HideHoverTips();
    }

    public static void Cleanup()
    {
        _focusedEndTurnButton = null;
        Clear();
    }

    /// <summary>Controls whether an active card or potion action owns the shared damage presentation surfaces.</summary>
    public static void SetActionDamageOverride(bool active)
    {
        var wasActive = _isActionDamageOverrideActive;
        _isActionDamageOverrideActive = active;

        if (wasActive && !active)
        {
            Refresh();
        }
    }

    private static bool ShouldShow(EndTurnPredictionDisplayMode displayMode)
    {
        return displayMode switch
        {
            EndTurnPredictionDisplayMode.EndTurnButtonHover => _focusedEndTurnButton != null,
            _ => true
        };
    }

    private static bool IsRefreshDeferred()
    {
        var action = RunManager.Instance.ActionExecutor.CurrentlyRunningAction;
        return action != null && ActionQueueSet.IsGameActionPlayerDriven(action);
    }

    private static void OnActionExecuted(GameAction _)
    {
        if (!_refreshPending || _isDeferredRefreshScheduled)
        {
            return;
        }

        // ActionExecutor raises AfterActionExecuted before clearing CurrentlyRunningAction. Recheck on the next
        // process frame so the completed action no longer keeps the pending refresh deferred.
        _isDeferredRefreshScheduled = true;
        Callable.From(FlushPendingRefresh).CallDeferred();
    }

    private static void FlushPendingRefresh()
    {
        _isDeferredRefreshScheduled = false;
        if (_isSubscribed && _refreshPending)
        {
            Refresh();
        }
    }

    private static void OnAboutToSwitchToEnemyTurn(CombatState _)
    {
        Clear();
    }

    private static void OnPlayerEndedTurn(Player _, bool __)
    {
        Refresh();
    }

    private static void OnPlayerUnendedTurn(Player _)
    {
        Refresh();
    }

    private static void OnCombatEnded(CombatRoom _)
    {
        Cleanup();
    }

    private static void OnCombatStateChanged(CombatState _)
    {
        Refresh();
    }

    private static void OnSettingsValueWritten(IModSettingsBinding binding)
    {
        if (ReferenceEquals(binding, SettingsUiBindings.DamagePredictionHealthBarColor))
        {
            DamagePredictionHealthBarForecast.RefreshActiveForecasts();
        }
        else if (IsEndTurnPredictionRefreshBinding(binding))
        {
            Refresh();
        }
    }

    private static bool IsEndTurnPredictionRefreshBinding(IModSettingsBinding binding)
    {
        return ReferenceEquals(binding, SettingsUiBindings.CombatDamagePredictionEnabled) ||
            ReferenceEquals(binding, SettingsUiBindings.RandomTargetAttackPredictionEnabled) ||
            ReferenceEquals(binding, SettingsUiBindings.OrbDamagePredictionEnabled) ||
            ReferenceEquals(binding, SettingsUiBindings.EndTurnPredictionEnabled) ||
            ReferenceEquals(binding, SettingsUiBindings.EndTurnPredictionDisplayMode) ||
            ReferenceEquals(binding, SettingsUiBindings.EndTurnHealthBarForecastDisplayMode);
    }
}

internal static class EndTurnButtonHoverTipHelper
{
    private static Control? _hoverTipOwner;

    public static void ShowHoverTips(NEndTurnButton endTurnButton, IEnumerable<IHoverTip> hoverTips)
    {
        HideHoverTips();

        var owner = CombatPredictionOverlay.ActiveIndicators
            .MinBy(static indicator => indicator.GetGlobalRect().Position.X);
        if (owner == null)
        {
            return;
        }

        var tipSet = NHoverTipSet.CreateAndShow(owner, hoverTips, HoverTip.GetHoverTipAlignment(owner, 0.5f));
        if (tipSet != null)
        {
            _hoverTipOwner = owner;
            AvoidHoverTipOverlap(tipSet, endTurnButton);
        }
    }

    public static void HideHoverTips()
    {
        if (_hoverTipOwner != null)
        {
            NHoverTipSet.Remove(_hoverTipOwner);
            _hoverTipOwner = null;
        }
    }

    private static void AvoidHoverTipOverlap(NHoverTipSet tipSet, Control avoidOwner)
    {
        if (!NHoverTipSet._activeHoverTips.TryGetValue(avoidOwner, out var avoidTipSet))
        {
            return;
        }

        var ourRect = GetHoverTipSetRect(tipSet);
        var avoidRect = GetHoverTipSetRect(avoidTipSet);
        if (!ourRect.HasArea() || !avoidRect.HasArea() || !ourRect.Intersects(avoidRect))
        {
            return;
        }

        var offset = ourRect.End.X - avoidRect.Position.X + 8f;
        if (offset <= 0f)
        {
            return;
        }

        MoveHoverTipSet(tipSet, Vector2.Left * offset);
    }

    private static Rect2 GetHoverTipSetRect(NHoverTipSet tipSet)
    {
        var textRect = tipSet._textHoverTipContainer.GetGlobalRect();
        var cardRect = tipSet._cardHoverTipContainer.GetGlobalRect();

        return textRect.HasArea() switch
        {
            true when cardRect.HasArea() => textRect.Merge(cardRect),
            true => textRect,
            _ => cardRect
        };
    }

    private static void MoveHoverTipSet(NHoverTipSet tipSet, Vector2 offset)
    {
        tipSet._textHoverTipContainer.GlobalPosition += offset;
        tipSet._cardHoverTipContainer.GlobalPosition += offset;
    }
}

[HarmonyPatch(typeof(NCombatRoom))]
internal static class EndTurnPredictionCombatRoomPatches
{
    [HarmonyPatch("_EnterTree")]
    [HarmonyPostfix]
    private static void Subscribe()
    {
        EndTurnPredictionController.Subscribe();
    }

    [HarmonyPatch("_ExitTree")]
    [HarmonyPostfix]
    private static void Unsubscribe()
    {
        EndTurnPredictionController.Unsubscribe();
    }
}

[HarmonyPatch(typeof(NEndTurnButton))]
internal static class EndTurnPredictionButtonPatches
{
    [HarmonyPatch("OnFocus")]
    [HarmonyPostfix]
    private static void OnFocus(NEndTurnButton __instance)
    {
        EndTurnPredictionController.OnEndTurnButtonFocused(__instance);
    }

    [HarmonyPatch("OnUnfocus")]
    [HarmonyPostfix]
    private static void OnUnfocus()
    {
        EndTurnPredictionController.OnEndTurnButtonUnfocused();
    }
}
