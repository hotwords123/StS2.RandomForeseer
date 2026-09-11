using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Entities.Cards;
using RandomForeseer.RandomForeseerCode.Data;

namespace RandomForeseer.RandomForeseerCode.InCombat.Nodes;

[ScriptPath("res://RandomForeseerCode/InCombat/Nodes/NCombatPredictionDamageIndicator.cs")]
internal sealed partial class NCombatPredictionDamageIndicator : MarginContainer
{
    private const string ScenePath = $"{Entry.ResPath}/scenes/combat_prediction_damage_indicator.tscn";

    private static readonly Color DefaultOutlineColor = StsColors.halfTransparentBlack;
    private static readonly Color BlockedOutlineColor = new("2B5A85");
    private static readonly Color LethalOutlineColor = new("900000");

    private Creature _target = null!;
    private HBoxContainer _sourceIcons = null!;
    private Label _damageLabel = null!;
    private bool _initialized;

    private DamagePredictionTarget? _prediction;
    private bool _hasRisk;

    private IEnumerable<IHoverTip> _hoverTips = [];
    private bool _isHovering;

    public static NCombatPredictionDamageIndicator Create(Creature target)
    {
        var indicator = GD.Load<PackedScene>(ScenePath).Instantiate<NCombatPredictionDamageIndicator>();
        indicator._target = target;
        return indicator;
    }

    public override void _Ready()
    {
        _sourceIcons = GetNode<HBoxContainer>("Content/SourceIcons");
        // DamageLabel styling in the scene mirrors res://scenes/combat/health_bar.tscn's HpLabel.
        _damageLabel = GetNode<Label>("Content/DamageLabel");

        if (_sourceIcons is null || _damageLabel is null)
        {
            Entry.Logger.Error("Failed to initialize NCombatPredictionDamageIndicator: missing child nodes.");
            Visible = false;
            return;
        }

        Connect(Control.SignalName.MouseEntered, Callable.From(OnMouseEntered));
        Connect(Control.SignalName.MouseExited, Callable.From(OnMouseExited));

        _initialized = true;
        ShowPrediction();
    }

    public void SetPrediction(DamagePredictionTarget prediction, bool hasRisk)
    {
        _prediction = prediction;
        _hasRisk = hasRisk;

        // Godot can report Ready even when our _Ready callback failed partway through.
        if (_initialized)
        {
            ShowPrediction();
        }
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
            var sourceIcon = NCombatPredictionSourceIcon.Create(GetIcon(source), hitCount);
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

        return hasRisk && ModData.Settings.ShowDriftWarnings ? $"{amountText}*" : amountText;
    }

    private static Texture2D GetIcon(AbstractModel source)
    {
        var icon = source switch
        {
            CardModel card => GetCardIcon(card),
            PotionModel potion => potion.Image,
            EnchantmentModel enchantment => enchantment.Icon,
            OrbModel orb => orb.Icon,
            PowerModel power => power.Icon,
            RelicModel relic => relic.Icon,
            _ => null
        };
        return icon ?? ModelDb.Power<StrengthPower>().Icon;
    }

    private static Texture2D? GetCardIcon(CardModel card)
    {
        if (!ResourceLoader.Exists(card.PortraitPath) || card.Portrait is not { } portrait)
        {
            return null;
        }

        var portraitSize = portrait.GetSize();
        if (card.Rarity is not CardRarity.Ancient || portraitSize.Y <= portraitSize.X)
        {
            return portrait;
        }

        var (atlas, region) = portrait switch
        {
            AtlasTexture atlasPortrait => (atlasPortrait.Atlas, atlasPortrait.Region),
            _ => (portrait, new Rect2(Vector2.Zero, portraitSize))
        };

        var cropOffset = new Vector2(region.Size.X * 28f / 299f, region.Size.Y * 47f / 421f);
        var cropSize = new Vector2(region.Size.X * 250f / 299f, region.Size.Y * 190f / 421f);
        return new AtlasTexture
        {
            Atlas = atlas,
            Region = new Rect2(region.Position + cropOffset, cropSize),
            FilterClip = true
        };
    }
}
