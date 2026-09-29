using Godot;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Assets;

namespace RandomForeseer.RandomForeseerCode.Common.Nodes;

[ScriptPath("res://RandomForeseerCode/Common/Nodes/NMegaLabel.cs")]
internal sealed partial class NMegaLabel : MegaLabel
{
    private const string LabelFontPath = "res://themes/kreon_bold_glyph_space_one.tres";

    public override void _Ready()
    {
        AddThemeFontOverride(ThemeConstants.Label.Font, PreloadManager.Cache.GetAsset<Font>(LabelFontPath));

        base._Ready();
    }
}
