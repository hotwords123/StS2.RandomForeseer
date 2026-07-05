using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.addons.mega_text;

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
    private MegaLabel _damageLabel = null!;
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
        _damageLabel = GetNode<MegaLabel>("Content/DamageLabel");

        Connect(SignalName.MouseEntered, Callable.From(OnMouseEntered));
        Connect(SignalName.MouseExited, Callable.From(OnMouseExited));
    }

    public void SetPrediction(DamagePredictionTarget prediction, bool hasRisk)
    {
        ClearSourceIcons();

        if (prediction.DamageLines.Count == 0)
        {
            Visible = false;
            Size = Vector2.Zero;
            CustomMinimumSize = Vector2.Zero;
            return;
        }

        var sourceGroups = prediction.DamageLines
            .GroupBy(static line => line.SourceModel?.Id)
            .Select(static group => (group.First().SourceModel, group.Count()));

        foreach (var (sourceModel, hitCount) in sourceGroups)
        {
            var sourceIcon = NCombatPredictionSourceIcon.Create(GetIcon(sourceModel), hitCount);
            _sourceIcons.AddChildSafely(sourceIcon);
        }

        var amountText = GetAmountText(prediction, hasRisk);
        var outlineColor = GetOutlineColor(prediction);

        _damageLabel.Text = amountText;
        _damageLabel.AddThemeColorOverride(ThemeConstants.Label.FontOutlineColor, outlineColor);

        Visible = true;
        CustomMinimumSize = Vector2.Zero;
        Size = GetCombinedMinimumSize();
    }

    public void SetHoverTips(IEnumerable<IHoverTip> hoverTips)
    {
        _hoverTips = hoverTips;
        if (_isHovering)
        {
            ShowHoverTips();
        }
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
        if (prediction.WasTargetKilled)
        {
            return LethalOutlineColor;
        }

        return prediction.TotalUnblockedDamage == 0 && prediction.TotalDamage > 0
            ? BlockedOutlineColor
            : DefaultOutlineColor;
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
}
