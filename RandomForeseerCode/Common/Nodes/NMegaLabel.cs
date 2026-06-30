using Godot;
using MegaCrit.Sts2.addons.mega_text;

namespace RandomForeseer.RandomForeseerCode.Common.Nodes;

[ScriptPath("res://RandomForeseerCode/Common/Nodes/NMegaLabel.cs")]
internal sealed partial class NMegaLabel : MegaLabel
{
    private const string LabelFontPath = "res://themes/kreon_bold_glyph_space_one.tres";

    private static readonly Font LabelFont = ResourceLoader.Load<Font>(
        LabelFontPath,
        cacheMode: ResourceLoader.CacheMode.Reuse);

    public override void _Ready()
    {
        AddThemeFontOverride(ThemeConstants.Label.Font, LabelFont);

        base._Ready();
    }
}
