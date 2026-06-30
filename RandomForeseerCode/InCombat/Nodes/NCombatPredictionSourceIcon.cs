using Godot;
using MegaCrit.Sts2.addons.mega_text;

namespace RandomForeseer.RandomForeseerCode.InCombat.Nodes;

// Mirrors the source icon/count layout from res://scenes/combat/power.tscn.
[ScriptPath("res://RandomForeseerCode/InCombat/Nodes/NCombatPredictionSourceIcon.cs")]
internal sealed partial class NCombatPredictionSourceIcon : Control
{
    private const string ScenePath = $"{Entry.ResPath}/scenes/combat_prediction_source_icon.tscn";

    private Texture2D _icon = null!;
    private int _hitCount;

    public static NCombatPredictionSourceIcon Create(Texture2D icon, int hitCount)
    {
        var sourceIcon = GD.Load<PackedScene>(ScenePath).Instantiate<NCombatPredictionSourceIcon>();
        sourceIcon._icon = icon;
        sourceIcon._hitCount = hitCount;
        return sourceIcon;
    }

    public override void _Ready()
    {
        GetNode<TextureRect>("Icon").Texture = _icon;
        GetNode<MegaLabel>("AmountLabel").SetTextAutoSize($"x{_hitCount}");
    }
}
