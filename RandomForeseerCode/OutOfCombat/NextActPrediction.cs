using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using RandomForeseer.RandomForeseerCode.OutOfCombat.Nodes;

namespace RandomForeseer.RandomForeseerCode.OutOfCombat;

internal static class NextActPrediction
{
    private static readonly ConditionalWeakTable<NTopBar, NextActPredictionIcons> IconsByTopBar = new();
    private static readonly List<NextActPredictionIcons> ActiveIcons = [];
    private static bool _isSubscribed;

    public static void Initialize(NTopBar topBar)
    {
        IconsByTopBar.GetValue(topBar, static key => new NextActPredictionIcons(key));

        if (!_isSubscribed)
        {
            _isSubscribed = true;

            RunManager.Instance.RoomExited += Hide;
            RunManager.Instance.ActEntered += Hide;
        }
    }

    public static void ShowIfEligible(bool isTerminal, IRunState runState)
    {
        if (!ShouldShow(isTerminal, runState))
        {
            Hide();
            return;
        }

        var nextAct = runState.Acts[runState.CurrentActIndex + 1];
        foreach (var icons in ActiveIcons.ToList())
        {
            icons.Show(nextAct);
        }
    }

    public static void Hide()
    {
        foreach (var icons in ActiveIcons.ToList())
        {
            icons.Hide();
        }
    }

    private static bool ShouldShow(bool isTerminal, IRunState runState)
    {
        return isTerminal &&
            RandomForeseerSettings.IsPredictionFeatureEnabled(RandomForeseerSettings.EnableNextActPrediction) &&
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

        var map = runState.Map;
        return map.SecondBossMapPoint != null
            ? currentCoord == map.SecondBossMapPoint.coord
            : currentCoord == map.BossMapPoint.coord;
    }

    private sealed class NextActPredictionIcons
    {
        private readonly NTopBar _topBar;
        private readonly NNextActPredictionIcon _ancientIcon;
        private readonly NNextActPredictionIcon _bossIcon;
        private bool _isVisible;

        public NextActPredictionIcons(NTopBar topBar)
        {
            _topBar = topBar;

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

            Subscribe();
        }

        public void Show(ActModel nextAct)
        {
            _isVisible = true;

            _ancientIcon.SetPrediction(nextAct);
            _ancientIcon.Visible = true;
            _ancientIcon.FocusMode = Control.FocusModeEnum.All;
            _ancientIcon.MouseFilter = Control.MouseFilterEnum.Stop;

            _bossIcon.SetPrediction(nextAct);
            _bossIcon.Visible = true;
            _bossIcon.FocusMode = Control.FocusModeEnum.All;
            _bossIcon.MouseFilter = Control.MouseFilterEnum.Stop;

            UpdateNavigation();
        }

        public void Hide()
        {
            if (!_isVisible)
            {
                return;
            }

            _isVisible = false;

            _ancientIcon.Visible = false;
            _ancientIcon.FocusMode = Control.FocusModeEnum.None;
            _ancientIcon.MouseFilter = Control.MouseFilterEnum.Ignore;

            _bossIcon.Visible = false;
            _bossIcon.FocusMode = Control.FocusModeEnum.None;
            _bossIcon.MouseFilter = Control.MouseFilterEnum.Ignore;

            UpdateNavigation();
        }

        private void Subscribe()
        {
            _ancientIcon.Connect(Node.SignalName.TreeExiting, Callable.From(Unsubscribe));
            _bossIcon.Connect(Node.SignalName.TreeExiting, Callable.From(Unsubscribe));

            ActiveIcons.Add(this);
        }

        private void Unsubscribe()
        {
            ActiveIcons.Remove(this);
        }

        private void UpdateNavigation()
        {
            if (_isVisible)
            {
                _topBar.FloorIcon.FocusNeighborRight = _ancientIcon.GetPath();
                _ancientIcon.FocusNeighborLeft = _topBar.FloorIcon.GetPath();
                _ancientIcon.FocusNeighborRight = _bossIcon.GetPath();
                _bossIcon.FocusNeighborLeft = _ancientIcon.GetPath();
                _bossIcon.FocusNeighborRight = _topBar.BossIcon.IsVisible()
                    ? _topBar.BossIcon.GetPath()
                    : _bossIcon.GetPath();
                _topBar.BossIcon.FocusNeighborLeft = _bossIcon.GetPath();
            }
            else
            {
                _topBar.FloorIcon.FocusNeighborRight = _topBar.BossIcon.GetPath();
                _topBar.BossIcon.FocusNeighborLeft = _topBar.FloorIcon.GetPath();
            }
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
        NextActPrediction.Initialize(__instance);
    }
}

[HarmonyPatch(typeof(NRewardsScreen))]
internal static class NextActPredictionRewardsScreenPatches
{
    [HarmonyPatch(nameof(NRewardsScreen.ShowScreen))]
    [HarmonyPostfix]
    private static void ShowPrediction(bool isTerminal, IRunState runState)
    {
        NextActPrediction.ShowIfEligible(isTerminal, runState);
    }
}
