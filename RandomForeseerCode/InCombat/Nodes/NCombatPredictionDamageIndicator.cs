using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Assets;

namespace RandomForeseer.InCombat.Nodes;

internal sealed partial class NCombatPredictionDamageIndicator : MarginContainer
{
    private static readonly Color DefaultOutlineColor = StsColors.halfTransparentBlack;
    private static readonly Color BlockedOutlineColor = new("2B5A85");
    private static readonly Color LethalOutlineColor = new("900000");

    private readonly Creature _target;

    private HBoxContainer _sourceIcons;
    private Label _damageLabel;

    private DamagePredictionTarget? _prediction;
    private bool _hasRisk;

    private IEnumerable<IHoverTip> _hoverTips = [];
    private bool _isHovering;

    public NCombatPredictionDamageIndicator(Creature target)
    {
        _target = target;

        ZIndex = 9;
        MouseFilter = MouseFilterEnum.Stop;
        AddThemeConstantOverride("margin_left", 10);
        AddThemeConstantOverride("margin_top", 4);
        AddThemeConstantOverride("margin_right", 10);
        AddThemeConstantOverride("margin_bottom", 4);

        var content = new HBoxContainer
        {
            Name = "Content",
            MouseFilter = MouseFilterEnum.Ignore,
            Alignment = BoxContainer.AlignmentMode.Center
        };
        content.AddThemeConstantOverride("separation", 0);
        _sourceIcons = new HBoxContainer
        {
            Name = "SourceIcons",
            MouseFilter = MouseFilterEnum.Ignore
        };
        _sourceIcons.AddThemeConstantOverride("separation", 0);
        content.AddChild(_sourceIcons);
        content.AddChild(new Control
        {
            Name = "NumberSpacer",
            CustomMinimumSize = new Vector2(10f, 40f),
            MouseFilter = MouseFilterEnum.Ignore
        });

        // Mirrors res://scenes/combat/health_bar.tscn's HpLabel styling.
        _damageLabel = new MegaLabel
        {
            Name = "DamageLabel",
            CustomMinimumSize = new Vector2(20f, 40f),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AutoSizeEnabled = false,
            MaxFontSize = 24
        };
        _damageLabel.AddThemeFontOverride(
            ThemeConstants.Label.Font,
            PreloadManager.Cache.GetAsset<Font>("res://themes/kreon_bold_glyph_space_one.tres"));
        _damageLabel.AddThemeColorOverride(ThemeConstants.Label.FontColor, StsColors.cream);
        _damageLabel.AddThemeColorOverride(ThemeConstants.Label.FontShadowColor, StsColors.quarterTransparentBlack);
        _damageLabel.AddThemeColorOverride(ThemeConstants.Label.FontOutlineColor, DefaultOutlineColor);
        _damageLabel.AddThemeConstantOverride("shadow_offset_x", 3);
        _damageLabel.AddThemeConstantOverride("shadow_offset_y", 3);
        _damageLabel.AddThemeConstantOverride("outline_size", 8);
        _damageLabel.AddThemeConstantOverride("shadow_outline_size", 0);
        _damageLabel.AddThemeFontSizeOverride(ThemeConstants.Label.FontSize, 24);
        content.AddChild(_damageLabel);
        AddChild(content);

        Connect(Control.SignalName.MouseEntered, Callable.From(OnMouseEntered));
        Connect(Control.SignalName.MouseExited, Callable.From(OnMouseExited));
    }

    public void SetPrediction(DamagePredictionTarget prediction, bool hasRisk)
    {
        _prediction = prediction;
        _hasRisk = hasRisk;

        ShowPrediction();
    }

    public void SetHoverTips(IEnumerable<IHoverTip> hoverTips)
    {
        _hoverTips = hoverTips;

        if (_isHovering)
        {
            ShowHoverTips();
        }
    }

    private void ShowPrediction()
    {
        ClearSourceIcons();

        if (_prediction is not { DamageLines.Count: > 0 } prediction)
        {
            Visible = false;
            Size = Vector2.Zero;
            CustomMinimumSize = Vector2.Zero;
            return;
        }

        var sourceGroups = prediction.DamageLines
            .GroupBy(static line => line.Source.Id)
            .Select(static group => (group.First().Source, group.Count()));

        foreach (var (source, hitCount) in sourceGroups)
        {
            var sourceIcon = new NCombatPredictionSourceIcon(source, hitCount);
            _sourceIcons.AddChildSafely(sourceIcon);
        }

        var amountText = GetAmountText(prediction, _hasRisk);
        var outlineColor = GetOutlineColor(prediction);

        _damageLabel.Text = amountText;
        _damageLabel.AddThemeColorOverride(ThemeConstants.Label.FontOutlineColor, outlineColor);

        Visible = true;
        CustomMinimumSize = Vector2.Zero;
        Size = GetCombinedMinimumSize();
    }

    private void ClearSourceIcons()
    {
        foreach (var child in _sourceIcons.GetChildren().ToList())
        {
            _sourceIcons.RemoveChildSafely(child);
            child.QueueFreeSafely();
        }
    }

    private void ShowHoverTips()
    {
        NCombatRoom.Instance?.GetCreatureNode(_target)?.ShowHoverTips(_hoverTips);
    }

    private void OnMouseEntered()
    {
        _isHovering = true;
        ShowHoverTips();
    }

    private void OnMouseExited()
    {
        _isHovering = false;
        NCombatRoom.Instance?.GetCreatureNode(_target)?.HideHoverTips();
    }

    private static Color GetOutlineColor(DamagePredictionTarget prediction)
    {
        return prediction switch
        {
            { WasTargetKilled: true } => LethalOutlineColor,
            { TotalUnblockedDamage: 0, TotalDamage: > 0 } => BlockedOutlineColor,
            _ => DefaultOutlineColor
        };
    }

    private static string GetAmountText(DamagePredictionTarget prediction, bool hasRisk)
    {
        var totalDamage = (int)prediction.TotalDamage;
        var totalUnblockedDamage = (int)prediction.TotalUnblockedDamage;

        var amountText = totalUnblockedDamage < totalDamage
            ? $"{totalDamage}({totalUnblockedDamage})"
            : totalDamage.ToString();

        return hasRisk ? $"{amountText}*" : amountText;
    }

}
