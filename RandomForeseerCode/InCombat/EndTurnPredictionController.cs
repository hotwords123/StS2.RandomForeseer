using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using RandomForeseer.RandomForeseerCode.Common;
using STS2RitsuLib.Settings;

namespace RandomForeseer.RandomForeseerCode.InCombat;

internal static class EndTurnPredictionController
{
    private static bool _isSubscribed;
    private static bool _isCardDamageOverrideActive;
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
        if (_isCardDamageOverrideActive)
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

        DamagePredictionResult prediction;

        try
        {
            prediction = EndTurnPrediction.PredictDamage();
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"End-turn prediction refresh failed: {ex}");
            Clear();
            return;
        }

        if (!prediction.HasTargets)
        {
            Clear();
            return;
        }

        EndTurnPredictionCreatureHoverTips.Set(prediction);

        if (ShouldShow(RandomForeseerSettings.EndTurnPredictionDisplayMode))
        {
            CombatPredictionOverlay.Show(prediction, EndTurnPredictionCreatureHoverTips.GetHoverTips);
        }
        else
        {
            CombatPredictionOverlay.Clear();
        }

        if (ShouldShow(RandomForeseerSettings.EndTurnHealthBarForecastDisplayMode))
        {
            DamagePredictionHealthBarForecast.Set(prediction);
        }
        else
        {
            DamagePredictionHealthBarForecast.Clear();
        }

        if (RandomForeseerSettings.EndTurnPredictionDisplayMode is EndTurnPredictionDisplayMode.EndTurnButtonHover &&
            _focusedEndTurnButton != null)
        {
            List<IHoverTip> hoverTips = [PredictionHoverTips.Text("end_turn_prediction_indicator")];
            PredictionHoverTips.AddDriftWarningIfNeeded(hoverTips, "end_turn", prediction.Risk);

            EndTurnButtonHoverTipHelper.ShowHoverTips(_focusedEndTurnButton, hoverTips);
        }
        else
        {
            EndTurnButtonHoverTipHelper.HideHoverTips();
        }
    }

    public static void Clear()
    {
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

    public static void SetCardDamageOverride(bool active)
    {
        var wasActive = _isCardDamageOverrideActive;
        _isCardDamageOverrideActive = active;

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
        if (RandomForeseerSettings.IsDamagePredictionHealthBarColorBinding(binding))
        {
            DamagePredictionHealthBarForecast.RefreshActiveForecasts();
        }
        else if (RandomForeseerSettings.IsEndTurnPredictionRefreshBinding(binding))
        {
            Refresh();
        }
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
