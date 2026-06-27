using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.addons.mega_text;
using RandomForeseer.Common;

namespace RandomForeseer.InCombat;

internal static class CombatPredictionOverlay
{
    private const float IntentGap = 6f;

    private static readonly Dictionary<Creature, NCombatPredictionDamageIndicator> Indicators = [];
    private static object? _source;
    private static CombatPredictionOverlayContent? _content;
    private static NCombatPredictionDamageIndicator? _activeHoverTipOwner;

    public static Action? AfterSourceCleared { get; set; }

    public static void Show(object source, CombatPredictionOverlayContent content)
    {
        _source = source;
        _content = content;

        var activeTargets = content.Targets.Select(static target => target.Target).ToHashSet();
        foreach (var (target, indicator) in Indicators.ToList())
        {
            if (!activeTargets.Contains(target))
            {
                indicator.QueueFreeSafely();
                Indicators.Remove(target);
            }
        }

        foreach (var target in content.Targets)
        {
            var indicator = GetOrCreateIndicator(target.Target);
            indicator?.SetPrediction(target, content.HasWarning);
        }

        RefreshPositions();
        if (_activeHoverTipOwner != null)
        {
            ShowIndicatorHoverTips();
        }
    }

    public static void Clear(object? source)
    {
        if (source != null && !ReferenceEquals(_source, source))
        {
            return;
        }

        var hadSource = _source != null;
        ClearIndicatorHoverTips();
        _source = null;
        _content = null;

        foreach (var indicator in Indicators.Values)
        {
            indicator.QueueFreeSafely();
        }
        Indicators.Clear();

        if (hadSource)
        {
            AfterSourceCleared?.Invoke();
        }
    }

    public static bool IsShowingDifferentSource(object source)
    {
        return _source != null && !ReferenceEquals(_source, source);
    }

    public static void ShowIndicatorHoverTips(Control? avoidOwner = null)
    {
        ClearIndicatorHoverTips();

        if (_content?.HoverTips is not { Count: > 0 } tips ||
            GetHoverTipOwnerIndicator() is not { } owner)
        {
            return;
        }

        _activeHoverTipOwner = owner;

        var tipSet = NHoverTipSet.CreateAndShow(owner, tips, HoverTip.GetHoverTipAlignment(owner, 0.25f));
        if (tipSet != null && avoidOwner != null)
        {
            AvoidHoverTipOverlap(tipSet, avoidOwner);
        }
    }

    public static void ClearIndicatorHoverTips()
    {
        if (_activeHoverTipOwner == null)
        {
            return;
        }

        NHoverTipSet.Remove(_activeHoverTipOwner);
        _activeHoverTipOwner = null;
    }

    public static void RefreshPositions()
    {
        if (NCombatRoom.Instance == null)
        {
            Clear(null);
            return;
        }

        foreach (var (target, indicator) in Indicators.ToList())
        {
            var creatureNode = NCombatRoom.Instance.GetCreatureNode(target);
            if (creatureNode == null || !indicator.IsInsideTree())
            {
                indicator.QueueFreeSafely();
                Indicators.Remove(target);
                continue;
            }

            var intentRect = creatureNode.IntentContainer.GetGlobalRect();
            var indicatorSize = indicator.GetGlobalRect().Size;
            indicator.GlobalPosition = new Vector2(
                intentRect.GetCenter().X - indicatorSize.X / 2f,
                intentRect.Position.Y - indicatorSize.Y - IntentGap);
        }
    }

    private static NCombatPredictionDamageIndicator? GetOrCreateIndicator(Creature target)
    {
        if (Indicators.TryGetValue(target, out var existing) && existing.IsInsideTree())
        {
            return existing;
        }

        var parent = NCombatRoom.Instance?.GetCreatureNode(target)?.GetParent();
        if (parent == null)
        {
            return null;
        }

        var indicator = new NCombatPredictionDamageIndicator();
        parent.AddChildSafely(indicator);
        Indicators[target] = indicator;
        return indicator;
    }

    private static NCombatPredictionDamageIndicator? GetHoverTipOwnerIndicator()
    {
        return Indicators.Values
            .Where(static indicator => indicator.IsInsideTree())
            .MinBy(static indicator => indicator.GetGlobalRect().Position.X);
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

internal sealed record CombatPredictionOverlayContent(
    IReadOnlyList<CombatPredictionTargetOverlay> Targets,
    IReadOnlyList<IHoverTip> HoverTips)
{
    public static CombatPredictionOverlayContent Empty { get; } = new([], []);

    public bool HasWarning => HoverTips.Any(PredictionHoverTips.IsPredictionWarningHoverTip);
}

internal sealed record CombatPredictionTargetOverlay(
    Creature Target,
    IReadOnlyList<CombatPredictionDamageLine> DamageLines,
    bool IsLethal)
{
    public decimal TotalDamage => DamageLines.Sum(static line => line.Damage);

    public decimal TotalUnblockedDamage => DamageLines.Sum(static line => line.UnblockedDamage);
}

internal sealed record CombatPredictionDamageLine(
    decimal Damage,
    decimal UnblockedDamage,
    AbstractModel? SourceModel);

internal sealed partial class NCombatPredictionDamageIndicator() : Control
{
    private const string LabelFontPath = "res://themes/kreon_bold_glyph_space_one.tres";
    private const int ShadowOffsetX = 6;
    private const int ShadowOffsetY = 4;
    private const int OutlineSize = 16;
    private const int ShadowOutlineSize = 0;
    private const float HorizontalPadding = 10f;
    private const float VerticalPadding = 4f;
    private const float IconGap = 6f;
    private const float NumberGap = 6f;
    private const int FontSize = 24;

    private static readonly Color DefaultFontColor = StsColors.cream;
    private static readonly Color DefaultOutlineColor = StsColors.halfTransparentBlack;
    private static readonly Color BlockedOutlineColor = new("1B3045");
    private static readonly Color LethalOutlineColor = new("900000");

    private static readonly Font LabelFont = ResourceLoader.Load<Font>(
        LabelFontPath,
        cacheMode: ResourceLoader.CacheMode.Reuse);

    private readonly HBoxContainer _content = new()
    {
        MouseFilter = MouseFilterEnum.Ignore,
        Alignment = BoxContainer.AlignmentMode.Center
    };

    private readonly MegaLabel _damageLabel = new()
    {
        AutoSizeEnabled = false,
        MaxFontSize = FontSize,
        MouseFilter = MouseFilterEnum.Ignore,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center
    };

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        // NCombatRoom.SceneContainer is z=-10, so local z=9 keeps this above combat
        // scene elements while staying below CombatUi and pause/capstone backstops.
        ZIndex = 9;
        Connect(SignalName.MouseEntered, Callable.From(OnMouseEntered));
        Connect(SignalName.MouseExited, Callable.From(OnMouseExited));
        this.AddChildSafely(_content);
        _content.AddThemeConstantOverride("separation", (int)IconGap);
        _damageLabel.AddThemeFontOverride(ThemeConstants.Label.Font, LabelFont);
        _damageLabel.AddThemeFontSizeOverride(ThemeConstants.Label.FontSize, FontSize);
        _damageLabel.AddThemeColorOverride(ThemeConstants.Label.FontShadowColor, StsColors.quarterTransparentBlack);
        _damageLabel.AddThemeColorOverride(ThemeConstants.Label.FontColor, DefaultFontColor);
        _damageLabel.AddThemeConstantOverride("shadow_offset_x", ShadowOffsetX);
        _damageLabel.AddThemeConstantOverride("shadow_offset_y", ShadowOffsetY);
        _damageLabel.AddThemeConstantOverride("outline_size", OutlineSize);
        _damageLabel.AddThemeConstantOverride("shadow_outline_size", ShadowOutlineSize);
    }

    public void SetPrediction(CombatPredictionTargetOverlay prediction, bool hasWarning)
    {
        foreach (var child in _content.GetChildren().OfType<Node>().ToList())
        {
            _content.RemoveChildSafely(child);
            if (child != _damageLabel)
            {
                child.QueueFreeSafely();
            }
        }

        var outlineColor = GetOutlineColor(prediction);
        var sourceGroups = prediction.DamageLines
            .GroupBy(static line => line.SourceModel?.Id)
            .Select(static group => new CombatPredictionSourceGroup(
                group.First().SourceModel,
                group.Count()))
            .ToList();

        foreach (var sourceGroup in sourceGroups)
        {
            _content.AddChildSafely(new NCombatPredictionSourceIcon(
                GetIcon(sourceGroup.SourceModel),
                sourceGroup.HitCount,
                LabelFont));
        }

        var amount = (int)prediction.TotalDamage;
        var amountText = hasWarning ? $"{amount}*" : amount.ToString();
        var labelWidth = MathF.Max(30f, amountText.Length * 16f);
        _damageLabel.CustomMinimumSize = new Vector2(labelWidth, NCombatPredictionSourceIcon.Height);
        _damageLabel.Size = _damageLabel.CustomMinimumSize;
        _damageLabel.Text = amountText;
        _damageLabel.AddThemeColorOverride(ThemeConstants.Label.FontOutlineColor, outlineColor);
        _content.AddChildSafely(_damageLabel);

        var iconCount = sourceGroups.Count;
        var contentWidth = iconCount * NCombatPredictionSourceIcon.SlotWidth +
            Math.Max(0, iconCount - 1) * IconGap +
            NumberGap +
            labelWidth;
        Size = new Vector2(contentWidth + HorizontalPadding * 2f, NCombatPredictionSourceIcon.Height + VerticalPadding * 2f);
        CustomMinimumSize = Size;

        _content.Position = new Vector2(HorizontalPadding, VerticalPadding);
        _content.Size = new Vector2(contentWidth, NCombatPredictionSourceIcon.Height);
    }

    private void OnMouseEntered()
    {
        CombatPredictionOverlay.ShowIndicatorHoverTips();
    }

    private void OnMouseExited()
    {
        CombatPredictionOverlay.ClearIndicatorHoverTips();
    }

    private static Color GetOutlineColor(CombatPredictionTargetOverlay prediction)
    {
        if (prediction.TotalUnblockedDamage <= 0)
        {
            return BlockedOutlineColor;
        }

        return prediction.IsLethal ? LethalOutlineColor : DefaultOutlineColor;
    }

    private static Texture2D GetIcon(AbstractModel? sourceModel)
    {
        return sourceModel switch
        {
            OrbModel orb => orb.Icon,
            PowerModel power => power.Icon,
            RelicModel relic => relic.Icon,
            _ => ModelDb.Power<StrengthPower>().Icon
        };
    }

    private sealed record CombatPredictionSourceGroup(AbstractModel? SourceModel, int HitCount);
}

internal sealed partial class NCombatPredictionSourceIcon(
    Texture2D icon,
    int hitCount,
    Font font) : Control
{
    // Mirrors the source icon/count layout from res://scenes/combat/power.tscn.
    public const float SlotWidth = 48f;
    public const float Height = 40f;

    private const float IconSize = 40f;
    private const int CountShadowOffsetX = 3;
    private const int CountShadowOffsetY = 3;
    private const int CountOutlineSize = 10;
    private const int ShadowOutlineSize = 0;
    private const int CountFontSize = 18;

    private static readonly Color DefaultFontColor = StsColors.cream;
    private static readonly Color CountOutlineColor = new(0.12f, 0.10208f, 0.0816f, 1f);

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        CustomMinimumSize = new Vector2(SlotWidth, Height);
        Size = CustomMinimumSize;

        var iconRect = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspect,
            Texture = icon,
            Size = Vector2.One * IconSize,
            PivotOffset = Vector2.One * IconSize / 2f,
            MouseFilter = MouseFilterEnum.Ignore
        };
        this.AddChildSafely(iconRect);

        var countLabel = new MegaLabel
        {
            AutoSizeEnabled = false,
            MaxFontSize = CountFontSize,
            Text = $"x{hitCount}",
            Size = new Vector2(100f, 23f),
            Position = new Vector2(-56f, 21f),
            MouseFilter = MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        countLabel.AddThemeFontOverride(ThemeConstants.Label.Font, font);
        countLabel.AddThemeFontSizeOverride(ThemeConstants.Label.FontSize, CountFontSize);
        countLabel.AddThemeColorOverride(ThemeConstants.Label.FontColor, DefaultFontColor);
        countLabel.AddThemeColorOverride(ThemeConstants.Label.FontShadowColor, StsColors.quarterTransparentBlack);
        countLabel.AddThemeColorOverride(ThemeConstants.Label.FontOutlineColor, CountOutlineColor);
        countLabel.AddThemeConstantOverride("shadow_offset_x", CountShadowOffsetX);
        countLabel.AddThemeConstantOverride("shadow_offset_y", CountShadowOffsetY);
        countLabel.AddThemeConstantOverride("outline_size", CountOutlineSize);
        countLabel.AddThemeConstantOverride("shadow_outline_size", ShadowOutlineSize);
        this.AddChildSafely(countLabel);
    }
}
