using Godot;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Assets;

namespace RandomForeseer.InCombat.Nodes;

// Mirrors the source icon/count layout from res://scenes/combat/power.tscn.
internal sealed partial class NCombatPredictionSourceIcon : Control
{
    public NCombatPredictionSourceIcon(Texture2D icon, int hitCount)
    {
        MouseFilter = MouseFilterEnum.Ignore;
        CustomMinimumSize = new Vector2(48f, 40f);
        Size = CustomMinimumSize;

        AddChild(new TextureRect
        {
            Name = "Icon",
            // Ignore the texture's minimum size before assigning the fixed icon size.
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            Texture = icon,
            TextureFilter = TextureFilterEnum.LinearWithMipmaps,
            Size = new Vector2(40f, 40f),
            PivotOffset = new Vector2(20f, 20f),
            MouseFilter = MouseFilterEnum.Ignore
        });

        var amountLabel = new MegaLabel
        {
            Name = "AmountLabel",
            Position = new Vector2(-56f, 21f),
            Size = new Vector2(100f, 23f),
            MouseFilter = MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            AutoSizeEnabled = false,
            MaxFontSize = 18
        };
        amountLabel.AddThemeFontOverride(
            ThemeConstants.Label.Font,
            PreloadManager.Cache.GetAsset<Font>("res://themes/kreon_bold_glyph_space_one.tres"));
        amountLabel.AddThemeColorOverride(ThemeConstants.Label.FontColor, StsColors.cream);
        amountLabel.AddThemeColorOverride(ThemeConstants.Label.FontShadowColor, new Color("00000030"));
        amountLabel.AddThemeColorOverride(ThemeConstants.Label.FontOutlineColor, new Color("1F1A15"));
        amountLabel.AddThemeConstantOverride("shadow_offset_x", 3);
        amountLabel.AddThemeConstantOverride("shadow_offset_y", 3);
        amountLabel.AddThemeConstantOverride("outline_size", 10);
        amountLabel.AddThemeConstantOverride("shadow_outline_size", 0);
        amountLabel.AddThemeFontSizeOverride(ThemeConstants.Label.FontSize, 18);
        AddChild(amountLabel);
        amountLabel.SetTextAutoSize($"x{hitCount}");
    }
}
