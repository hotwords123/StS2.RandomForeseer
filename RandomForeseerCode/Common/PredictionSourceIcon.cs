using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace RandomForeseer.Common;

/// <summary>Shared source artwork, card badges, and silhouette shadows for prediction surfaces.</summary>
internal static class PredictionSourceIcon
{
    private static readonly Vector2[] OutlineOffsets =
        [new(-1f, 0f), new(1f, 0f), new(0f, -1f), new(0f, 1f)];

    private static readonly Lazy<Shader> PortraitMaskShader = new(() =>
        ResourceLoader.Load<Shader>($"{Entry.ResPath}/shaders/prediction_portrait_mask.gdshader"));

    public static Control Create(AbstractModel source, float size)
    {
        var root = new Control
        {
            Size = new Vector2(size, size),
            CustomMinimumSize = new Vector2(size, size),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        var icon = GetIcon(source);
        if (source is CardModel card)
        {
            AddCardPortrait(root, card, icon, size);
        }
        else
        {
            AddTexture(root, icon, new Vector2(2f, 3f), size, new Color(0f, 0f, 0f, 0.4f));
            foreach (var offset in OutlineOffsets)
            {
                AddTexture(root, icon, offset, size, new Color(0.06f, 0.04f, 0.03f, 0.4f));
            }

            AddTexture(root, icon, Vector2.Zero, size, Colors.White);
        }

        return root;
    }

    private static void AddCardPortrait(Control parent, CardModel card, Texture2D portrait, float size)
    {
        // Mirrors NCard's PortraitBorder, Portrait and TitleBanner rectangles as one uniformly scaled group.
        var scale = size / 327f;
        var groupSize = new Vector2(size, 253f * scale);
        var groupPosition = (parent.Size - groupSize) / 2f;
        var frameSize = new Vector2(275f, 210f) * scale;
        var position = groupPosition + new Vector2(25.5f, 43f) * scale;
        var bannerSize = new Vector2(327f, 83f) * scale;
        var borderTexture = card.PortraitBorder;

        AddSizedTexture(
            parent,
            borderTexture,
            position + new Vector2(2f, 3f),
            frameSize,
            new Color(0f, 0f, 0f, 0.5f));

        var art = AddSizedTexture(
            parent,
            portrait,
            groupPosition + new Vector2(38f, 39f) * scale,
            new Vector2(250f, 190f) * scale,
            Colors.White);
        art.Material = CreatePortraitMask(card, portrait);
        var border = AddSizedTexture(parent, borderTexture, position, frameSize, Colors.White);
        border.Material = card.BannerMaterial;

        // NCard uses its separate AncientBanner scene node, not CardModel.BannerTexture, for ancient cards.
        var bannerTexture = card.Rarity is CardRarity.Ancient
            ? ResourceLoader.Load<Texture2D>("res://images/atlases/ui_atlas.sprites/card/ancient_banner.tres")
            : card.BannerTexture;
        AddSizedTexture(
            parent,
            bannerTexture,
            groupPosition + new Vector2(2f, 3f),
            bannerSize,
            new Color(0f, 0f, 0f, 0.5f));
        var banner = AddSizedTexture(parent, bannerTexture, groupPosition, bannerSize, Colors.White);
        banner.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
        banner.Material = card.Rarity is CardRarity.Ancient ? null : card.BannerMaterial;
    }

    private static ShaderMaterial CreatePortraitMask(CardModel card, Texture2D portrait)
    {
        // Vanilla draws the full card frame over the portrait. Its transparent opening is the portrait mask.
        // Ancient cards have no ordinary frame, so use the equivalent frame for their card type.
        var frame = card.Rarity is CardRarity.Ancient
            ? ResourceLoader.Load<Texture2D>(ImageHelper.GetImagePath(
                $"atlases/ui_atlas.sprites/card/card_frame_{GetFrameType(card.Type)}_s.tres"))
            : card.Frame;
        var (frameAtlas, frameRegion) = GetTextureRegion(frame);
        var (_, portraitRegion) = GetTextureRegion(portrait);
        var material = new ShaderMaterial { Shader = PortraitMaskShader.Value };
        material.SetShaderParameter("frame_texture", frameAtlas);
        material.SetShaderParameter("frame_region", frameRegion);
        material.SetShaderParameter("portrait_region", portraitRegion);
        return material;
    }

    private static string GetFrameType(CardType type) => type switch
    {
        CardType.Attack => "attack",
        CardType.Power => "power",
        CardType.Quest => "quest",
        _ => "skill"
    };

    private static (Texture2D Texture, Vector4 Region) GetTextureRegion(Texture2D texture)
    {
        if (texture is AtlasTexture { Atlas: var atlas, Region: var region })
        {
            var size = atlas.GetSize();
            return (atlas, new Vector4(
                region.Position.X / size.X,
                region.Position.Y / size.Y,
                region.Size.X / size.X,
                region.Size.Y / size.Y));
        }

        return (texture, new Vector4(0f, 0f, 1f, 1f));
    }

    private static TextureRect AddSizedTexture(
        Control parent,
        Texture2D texture,
        Vector2 position,
        Vector2 size,
        Color color)
    {
        var node = new TextureRect
        {
            // Set the sizing policy before the texture: its native minimum otherwise enlarges Size.
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            Texture = texture,
            Position = position,
            Size = size,
            SelfModulate = color,
            TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        parent.AddChild(node);
        return node;
    }

    private static void AddTexture(
        Control parent,
        Texture2D texture,
        Vector2 position,
        float size,
        Color color)
    {
        parent.AddChild(new TextureRect
        {
            // Set the sizing policy before the texture: its native minimum otherwise enlarges Size.
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            Texture = texture,
            Position = position,
            Size = new Vector2(size, size),
            SelfModulate = color,
            TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps,
            MouseFilter = Control.MouseFilterEnum.Ignore
        });
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
