using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using RandomForeseer.Common;
using STS2RitsuLib.Settings;

namespace RandomForeseer.InCombat;

internal static class EndTurnPredictionController
{
    private static readonly object Source = new();

    private static bool _isSubscribed;
    private static bool _isEndTurnButtonFocused;

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
        CombatPredictionOverlay.AfterSourceCleared += Refresh;
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
        CombatPredictionOverlay.AfterSourceCleared -= Refresh;
        ModSettingsBindingWriteEvents.ValueWritten -= OnSettingsValueWritten;

        _isSubscribed = false;
        Clear();
    }

    public static void OnEndTurnButtonFocused(NEndTurnButton endTurnButton)
    {
        _isEndTurnButtonFocused = true;
        Refresh();
        if (RandomForeseerSettings.EndTurnPredictionDisplayMode == EndTurnPredictionDisplayMode.EndTurnButtonHover)
        {
            CombatPredictionOverlay.ShowIndicatorHoverTips(endTurnButton);
        }
    }

    public static void OnEndTurnButtonUnfocused()
    {
        _isEndTurnButtonFocused = false;
        Refresh();
    }

    public static void Refresh()
    {
        if (NCombatRoom.Instance == null || !EndTurnPrediction.ShouldPredict())
        {
            Clear();
            return;
        }

        var shouldShowOverlay = ShouldShow(RandomForeseerSettings.EndTurnPredictionDisplayMode);
        var shouldShowHealthBarForecast = ShouldShow(RandomForeseerSettings.EndTurnHealthBarForecastDisplayMode);

        if (!shouldShowOverlay)
        {
            CombatPredictionOverlay.Clear(Source);
        }

        if (!shouldShowHealthBarForecast)
        {
            CombatPredictionHealthBarForecast.Clear(Source);
        }

        var canShowOverlay = shouldShowOverlay &&
            !CombatPredictionOverlay.IsShowingDifferentSource(Source);
        var canShowHealthBarForecast = shouldShowHealthBarForecast &&
            !CombatPredictionHealthBarForecast.IsShowingDifferentSource(Source);

        try
        {
            if (EndTurnPrediction.PredictDamage() is not { HasTargets: true } content)
            {
                CombatPredictionOverlay.Clear(Source);
                CombatPredictionHealthBarForecast.Clear(Source);
                return;
            }

            var hoverTips = PredictionHoverTips.Text("end_turn_prediction_indicator").ToList();
            PredictionHoverTips.AddDriftWarningIfNeeded(hoverTips, "end_turn", content.Risk);

            if (canShowHealthBarForecast)
            {
                CombatPredictionHealthBarForecast.Set(Source, content);
            }

            if (canShowOverlay)
            {
                CombatPredictionOverlay.Show(Source, content, hoverTips);
            }
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"End-turn prediction refresh failed: {ex}");
            CombatPredictionOverlay.Clear(Source);
            CombatPredictionHealthBarForecast.Clear(Source);
        }
    }

    public static void Clear()
    {
        _isEndTurnButtonFocused = false;
        CombatPredictionOverlay.Clear(Source);
        CombatPredictionHealthBarForecast.Clear(Source);
    }

    private static bool ShouldShow(EndTurnPredictionDisplayMode displayMode)
    {
        return displayMode switch
        {
            EndTurnPredictionDisplayMode.EndTurnButtonHover => _isEndTurnButtonFocused,
            _ => true
        };
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
        Clear();
    }

    private static void OnCombatStateChanged(CombatState _)
    {
        Refresh();
    }

    private static void OnSettingsValueWritten(IModSettingsBinding binding)
    {
        if (RandomForeseerSettings.IsEndTurnPredictionRefreshBinding(binding))
        {
            Refresh();
        }
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
