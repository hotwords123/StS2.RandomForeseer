using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using RandomForeseer.Settings;
using RandomForeseer.Telemetry;
using STS2RitsuLib.Settings;

namespace RandomForeseer.InCombat;

internal static class EndTurnPredictionController
{
    private static bool _isSubscribed;
    private static bool _isDeferredRefreshScheduled;
    private static bool _refreshPending;

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
        RefreshSafely();
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

    public static void OnEndTurnButtonFocused(NEndTurnButton endTurnButton) =>
        RunSafely(() => CombatPredictionProjectionController.SetEndTurnButton(endTurnButton));

    public static void OnEndTurnButtonUnfocused() =>
        RunSafely(() => CombatPredictionProjectionController.SetEndTurnButton(null));

    private static void RunSafely(Action refresh)
    {
        try
        {
            refresh();
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"End-turn prediction refresh failed: {ex}");
            ModTelemetry.CaptureException(ex, "end_turn_prediction", "refresh");
        }
    }

    private static void RefreshSafely() => RunSafely(Refresh);

    private static void Refresh()
    {
        if (NCombatRoom.Instance == null || !EndTurnPrediction.ShouldPredict())
        {
            _refreshPending = false;
            CombatPredictionProjectionController.SetEndTurn(null);
            return;
        }

        if (ShouldDeferRefresh())
        {
            _refreshPending = true;
            return;
        }

        _refreshPending = false;

        CombatPredictionProjection? projection = null;
        try
        {
            projection = EndTurnPrediction.Predict();
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"End-turn prediction refresh failed: {ex}");
            ModTelemetry.CaptureException(ex, "end_turn_prediction", "refresh");
        }

        CombatPredictionProjectionController.SetEndTurn(projection);
    }

    private static void Clear()
    {
        _refreshPending = false;
        CombatPredictionProjectionController.ClearAll();
    }

    private static void Cleanup()
    {
        Clear();
        CombatPredictionProjectionController.SetEndTurnButton(null);
    }

    private static bool ShouldDeferRefresh()
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
            RefreshSafely();
        }
    }

    private static void OnAboutToSwitchToEnemyTurn(CombatState _) => Clear();

    private static void OnPlayerEndedTurn(Player _, bool __) => RefreshSafely();

    private static void OnPlayerUnendedTurn(Player _) => RefreshSafely();

    private static void OnCombatEnded(CombatRoom _) => Cleanup();

    private static void OnCombatStateChanged(CombatState _) => RefreshSafely();

    private static void OnSettingsValueWritten(IModSettingsBinding binding)
    {
        if (ReferenceEquals(binding, SettingsUiBindings.DamagePredictionHealthBarColor))
        {
            DamagePredictionHealthBarForecast.RefreshActiveForecasts();
        }
        else if (IsDisplayModeBinding(binding))
        {
            RunSafely(CombatPredictionProjectionController.RefreshEndTurnDisplay);
        }
        else if (IsPredictionRelatedBinding(binding))
        {
            RefreshSafely();
        }
    }

    private static bool IsDisplayModeBinding(IModSettingsBinding binding) =>
        ReferenceEquals(binding, SettingsUiBindings.EndTurnDamageIndicatorDisplayMode) ||
        ReferenceEquals(binding, SettingsUiBindings.EndTurnHealthBarForecastDisplayMode) ||
        ReferenceEquals(binding, SettingsUiBindings.EndTurnCardHighlightDisplayMode);

    private static bool IsPredictionRelatedBinding(IModSettingsBinding binding) =>
        ReferenceEquals(binding, SettingsUiBindings.SingleplayerPredictionEnabled) ||
        ReferenceEquals(binding, SettingsUiBindings.MultiplayerPredictionEnabled) ||
        ReferenceEquals(binding, SettingsUiBindings.EndTurnPredictionEnabled) ||
        ReferenceEquals(binding, SettingsUiBindings.CombatCardSelectionPredictionEnabled) ||
        ReferenceEquals(binding, SettingsUiBindings.CombatDamagePredictionEnabled) ||
        ReferenceEquals(binding, SettingsUiBindings.OrbDamagePredictionEnabled) ||
        ReferenceEquals(binding, SettingsUiBindings.RandomTargetAttackPredictionEnabled);
}
