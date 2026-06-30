using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Rooms;
using RandomForeseer.RandomForeseerCode.Common;

namespace RandomForeseer.RandomForeseerCode.OutOfCombat.Nodes;

internal enum NextActPredictionIconKind
{
    Ancient,
    Boss
}

[ScriptPath("res://RandomForeseerCode/OutOfCombat/Nodes/NNextActPredictionIcon.cs")]
internal sealed partial class NNextActPredictionIcon : NClickableControl
{
    private const string ScenePath = $"{Entry.ResPath}/scenes/next_act_prediction_icon.tscn";
    private const string SecondBossIconScenePath = "res://scenes/ui/top_bar/second_boss_icon.tscn";
    private static readonly StringName TintColor = new("tint_color");

    private NextActPredictionIconKind _kind;
    private HoverTip? _hoverTip;

    private TextureRect _icon = null!;
    private TextureRect _outline = null!;
    private TextureRect? _secondBossIcon;
    private TextureRect? _secondBossIconOutline;

    public static NNextActPredictionIcon Create(NextActPredictionIconKind kind)
    {
        var icon = GD.Load<PackedScene>(ScenePath).Instantiate<NNextActPredictionIcon>();
        icon._kind = kind;
        return icon;
    }

    public override void _Ready()
    {
        // Mirrors res://scenes/ui/top_bar.tscn's BossIcon node structure.
        _icon = GetNode<TextureRect>("Icon");
        _outline = GetNode<TextureRect>("Icon/Outline");
        ConnectSignals();
    }

    public void SetPrediction(ActModel nextAct)
    {
        _hoverTip = CreateHoverTip(nextAct, _kind);

        var (pointType, roomType, modelId) = _kind switch
        {
            NextActPredictionIconKind.Ancient => (MapPointType.Ancient, RoomType.Event, nextAct.Ancient.Id),
            NextActPredictionIconKind.Boss => (MapPointType.Boss, RoomType.Boss, nextAct.BossEncounter.Id),
            _ => throw new InvalidOperationException($"Unknown NextActPredictionIconKind: {_kind}")
        };
        SetRoomIconAndOutline(pointType, roomType, modelId, _icon, _outline);

        RefreshSecondBossIcon(nextAct);
    }

    protected override void OnFocus()
    {
        if (_hoverTip == null)
        {
            return;
        }

        NHoverTipSet.CreateAndShow(this, _hoverTip)?.SetGlobalPosition(_icon.GlobalPosition + new Vector2(0f, Size.Y + 20f));
    }

    protected override void OnUnfocus()
    {
        NHoverTipSet.Remove(this);
    }

    private void RefreshSecondBossIcon(ActModel nextAct)
    {
        if (_kind != NextActPredictionIconKind.Boss || nextAct.SecondBossEncounter == null)
        {
            _secondBossIcon?.Visible = false;
            _secondBossIconOutline?.Visible = false;
            return;
        }

        if (_secondBossIcon == null)
        {
            // Mirrors NTopBarBossIcon.RefreshBossIcon's second-boss overlay.
            _secondBossIcon = GD.Load<PackedScene>(SecondBossIconScenePath).Instantiate<TextureRect>();
            _secondBossIconOutline = _secondBossIcon.GetNode<TextureRect>("%Outline");
            _secondBossIcon.MouseFilter = MouseFilterEnum.Pass;
            _secondBossIconOutline.MouseFilter = MouseFilterEnum.Pass;
            _icon.AddChildSafely(_secondBossIcon);
            _secondBossIcon.Position = new Vector2(30f, 22f);
        }

        SetRoomIconAndOutline(
            MapPointType.Boss,
            RoomType.Boss,
            nextAct.SecondBossEncounter.Id,
            _secondBossIcon,
            _secondBossIconOutline!);

        RefreshSecondBossIconColor(nextAct);

        _secondBossIcon.Visible = true;
        _secondBossIconOutline!.Visible = true;
    }

    private void RefreshSecondBossIconColor(ActModel nextAct)
    {
        if (_secondBossIcon?.Material is not ShaderMaterial iconMaterial ||
            _secondBossIconOutline?.Material is not ShaderMaterial outlineMaterial)
        {
            return;
        }

        var color = nextAct.MapUntraveledColor;
        var tint = new Vector3(color.R, color.G, color.B);
        iconMaterial.SetShaderParameter(TintColor, tint);
        outlineMaterial.SetShaderParameter(TintColor, tint);
    }

    private static void SetRoomIconAndOutline(
        MapPointType pointType,
        RoomType roomType,
        ModelId? modelId,
        TextureRect icon,
        TextureRect outline)
    {
        var roomIconPath = ImageHelper.GetRoomIconPath(pointType, roomType, modelId);
        if (roomIconPath != null)
        {
            icon.Texture = PreloadManager.Cache.GetTexture2D(roomIconPath);
        }

        var roomIconOutlinePath = ImageHelper.GetRoomIconOutlinePath(pointType, roomType, modelId);
        if (roomIconOutlinePath != null)
        {
            outline.Texture = PreloadManager.Cache.GetTexture2D(roomIconOutlinePath);
        }
    }

    private static HoverTip CreateHoverTip(ActModel nextAct, NextActPredictionIconKind kind)
    {
        return kind switch
        {
            NextActPredictionIconKind.Ancient => CreateAncientHoverTip(nextAct),
            NextActPredictionIconKind.Boss => CreateBossHoverTip(nextAct),
            _ => throw new InvalidOperationException($"Unknown NextActPredictionIconKind: {kind}")
        };
    }

    private static HoverTip CreateAncientHoverTip(ActModel nextAct)
    {
        return PredictionHoverTips.Text(
            "next_act_ancient_prediction",
            description => description.Add("Ancient", nextAct.Ancient.Title));
    }

    private static HoverTip CreateBossHoverTip(ActModel nextAct)
    {
        List<string> bossNames = [nextAct.BossEncounter.Title.GetFormattedText()];
        if (nextAct.SecondBossEncounter != null)
        {
            bossNames.Add(nextAct.SecondBossEncounter.Title.GetFormattedText());
        }

        return PredictionHoverTips.Text(
            "next_act_boss_prediction",
            description => description.Add("BossNames", bossNames));
    }
}
