using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using RandomForeseer.RandomForeseerCode.Data;
using RandomForeseer.RandomForeseerCode.OutOfCombat.Nodes;
using RandomForeseer.RandomForeseerCode.Telemetry;

namespace RandomForeseer.RandomForeseerCode.OutOfCombat;

internal static class NextActPrediction
{
    private static NextActPredictionIcons? _icons;
    private static bool _isSubscribed;

    public static void Initialize(NTopBar topBar)
    {
        if (ReferenceEquals(_icons?.TopBar, topBar))
        {
            return;
        }

        _icons = new NextActPredictionIcons(topBar);

        topBar.Connect(Node.SignalName.TreeExiting, Callable.From(() => Release(topBar)));

        if (!_isSubscribed)
        {
            _isSubscribed = true;

            RunManager.Instance.RoomExited += Hide;
            RunManager.Instance.ActEntered += Hide;
        }
    }

    public static void ShowIfEligible(bool isTerminal, IRunState runState)
    {
        if (_icons is null || !ShouldShow(isTerminal, runState))
        {
            Hide();
            return;
        }

        var nextAct = runState.Acts[runState.CurrentActIndex + 1];
        _icons.ShowIcons(nextAct);
    }

    public static void Hide() => _icons?.HideIcons();

    private static bool ShouldShow(bool isTerminal, IRunState runState)
    {
        var settings = ModData.Settings;
        return isTerminal &&
            settings.IsPredictionEnabled && settings.NextActPredictionEnabled &&
            runState.CurrentActIndex + 1 < runState.Acts.Count &&
            runState.CurrentRoom?.RoomType == RoomType.Boss &&
            IsActEndingBoss(runState);
    }

    private static bool IsActEndingBoss(IRunState runState)
    {
        var currentCoord = runState.CurrentMapCoord;
        if (currentCoord == null)
        {
            return false;
        }

        var finalBossMapPoint = runState.Map.SecondBossMapPoint ?? runState.Map.BossMapPoint;
        return currentCoord == finalBossMapPoint.coord;
    }

    private static void Release(NTopBar topBar)
    {
        if (ReferenceEquals(_icons?.TopBar, topBar))
        {
            // The icons are children of the top bar and are freed with its scene. Drop the controller reference so a
            // subsequent run can initialize its own top bar without retaining the previous Godot objects.
            _icons = null;
        }
    }

    private sealed class NextActPredictionIcons
    {
        private readonly NNextActPredictionIcon _ancientIcon;
        private readonly NNextActPredictionIcon _bossIcon;

        public NTopBar TopBar { get; }

        public NextActPredictionIcons(NTopBar topBar)
        {
            TopBar = topBar;

            _ancientIcon = NNextActPredictionIcon.Create(NextActPredictionIconKind.Ancient);
            _ancientIcon.Name = $"{Entry.ModId}_NextActPrediction_AncientIcon";
            _ancientIcon.Visible = false;

            _bossIcon = NNextActPredictionIcon.Create(NextActPredictionIconKind.Boss);
            _bossIcon.Name = $"{Entry.ModId}_NextActPrediction_BossIcon";
            _bossIcon.Visible = false;

            var roomIcons = topBar.FloorIcon.GetParent();
            roomIcons.AddChildSafely(_ancientIcon);
            roomIcons.AddChildSafely(_bossIcon);
            roomIcons.MoveChildSafely(_ancientIcon, topBar.FloorIcon.GetIndex() + 1);
            roomIcons.MoveChildSafely(_bossIcon, _ancientIcon.GetIndex() + 1);

            _ancientIcon.FocusNeighborTop = _ancientIcon.GetPath();
            _bossIcon.FocusNeighborTop = _bossIcon.GetPath();
        }

        public void ShowIcons(ActModel nextAct)
        {
            if (nextAct._rooms._ancient is not null)
            {
                _ancientIcon.SetPrediction(nextAct);
                ShowIcon(_ancientIcon);
            }
            else
            {
                HideIcon(_ancientIcon);
            }

            if (nextAct._rooms._boss is not null)
            {
                _bossIcon.SetPrediction(nextAct);
                ShowIcon(_bossIcon);
            }
            else
            {
                HideIcon(_bossIcon);
            }

            UpdateNavigation();
        }

        public void HideIcons()
        {
            HideIcon(_ancientIcon);
            HideIcon(_bossIcon);
            UpdateNavigation();
        }

        private static void ShowIcon(NNextActPredictionIcon icon)
        {
            icon.Visible = true;
            icon.FocusMode = Control.FocusModeEnum.All;
            icon.MouseFilter = Control.MouseFilterEnum.Stop;
        }

        private static void HideIcon(NNextActPredictionIcon icon)
        {
            icon.Visible = false;
            icon.FocusMode = Control.FocusModeEnum.None;
            icon.MouseFilter = Control.MouseFilterEnum.Ignore;
        }

        private void UpdateNavigation()
        {
            if (!_ancientIcon.Visible && !_bossIcon.Visible)
            {
                TopBar.FloorIcon.FocusNeighborRight = TopBar.BossIcon.GetPath();
                TopBar.BossIcon.FocusNeighborLeft = TopBar.FloorIcon.GetPath();
                return;
            }

            var firstIcon = _ancientIcon.Visible ? _ancientIcon : _bossIcon;
            var lastIcon = _bossIcon.Visible ? _bossIcon : _ancientIcon;

            TopBar.FloorIcon.FocusNeighborRight = firstIcon.GetPath();
            firstIcon.FocusNeighborLeft = TopBar.FloorIcon.GetPath();

            if (_ancientIcon.Visible && _bossIcon.Visible)
            {
                _ancientIcon.FocusNeighborRight = _bossIcon.GetPath();
                _bossIcon.FocusNeighborLeft = _ancientIcon.GetPath();
            }

            lastIcon.FocusNeighborRight = TopBar.BossIcon.IsVisible()
                ? TopBar.BossIcon.GetPath()
                : lastIcon.GetPath();
            TopBar.BossIcon.FocusNeighborLeft = lastIcon.GetPath();
        }
    }
}

[HarmonyPatch(typeof(NTopBar))]
internal static class NextActPredictionTopBarPatches
{
    [HarmonyPatch(nameof(NTopBar.Initialize))]
    [HarmonyPostfix]
    private static void Initialize(NTopBar __instance)
    {
        try
        {
            NextActPrediction.Initialize(__instance);
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"Next-Act prediction failed to initialize: {ex}");
            ModTelemetry.CaptureException(ex, "next_act_prediction", "initialize");
        }
    }
}

[HarmonyPatch(typeof(NRewardsScreen))]
internal static class NextActPredictionRewardsScreenPatches
{
    [HarmonyPatch(nameof(NRewardsScreen.ShowScreen))]
    [HarmonyPostfix]
    private static void ShowPrediction(bool isTerminal, IRunState runState)
    {
        try
        {
            NextActPrediction.ShowIfEligible(isTerminal, runState);
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"Next-Act prediction failed on the rewards screen: {ex}");
            ModTelemetry.CaptureException(ex, "next_act_prediction", "show_on_rewards_screen");
        }
    }
}
