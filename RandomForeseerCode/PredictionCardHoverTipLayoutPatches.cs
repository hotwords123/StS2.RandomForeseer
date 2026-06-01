using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.HoverTips;

namespace RandomForeseer;

[HarmonyPatch(typeof(NHoverTipCardContainer))]
internal static class PredictionCardHoverTipLayoutPatches
{
    internal static readonly StringName PredictionCardMetaKey = $"{Entry.ModId}_PredictionCard";
    private const float Padding = 4f;
    private const float ViewportMargin = 12f;
    private const float TopGap = 12f;
    private static readonly ConditionalWeakTable<NHoverTipCardContainer, SourceRect> SourceRects = [];

    [HarmonyPatch(nameof(NHoverTipCardContainer.Add))]
    [HarmonyPostfix]
    private static void MarkPredictionCardTip(NHoverTipCardContainer __instance, CardHoverTip cardTip)
    {
        if (cardTip is not PredictionCardHoverTip)
        {
            return;
        }

        var control = __instance.GetChildren().OfType<Control>().LastOrDefault();
        control?.SetMeta(PredictionCardMetaKey, Variant.From(true));
    }

    public static void RecordSourceRect(NHoverTipCardContainer container, Rect2 sourceRect)
    {
        SourceRects.Remove(container);
        SourceRects.Add(container, new SourceRect(sourceRect));
    }

    [HarmonyPatch(nameof(NHoverTipCardContainer.LayoutResizeAndReposition))]
    [HarmonyPrefix]
    private static bool LayoutPredictionCardTips(
        NHoverTipCardContainer __instance,
        Vector2 globalStartLocation,
        HoverTipAlignment alignment)
    {
        var tips = __instance.GetChildren().OfType<Control>().ToList();
        if (!tips.Any(tip => tip.HasMeta(PredictionCardMetaKey)))
        {
            return true;
        }

        var game = NGame.Instance;
        if (game == null)
        {
            return true;
        }

        var viewportSize = game.GetViewportRect().Size;
        var naturalSize = ApplyHorizontalLayout(tips, scale: 1f);
        var sidePosition = GetSidePosition(globalStartLocation, alignment, naturalSize);

        // Preserve the vanilla side placement when it fits; small prediction sets should behave exactly like before.
        if (FitsWithinViewport(sidePosition, naturalSize, viewportSize))
        {
            __instance.Size = naturalSize;
            __instance.GlobalPosition = sidePosition;
            return false;
        }

        if (!SourceRects.TryGetValue(__instance, out var sourceRect))
        {
            var sideScale = Mathf.Min(1f, (viewportSize.X - ViewportMargin * 2f) / naturalSize.X);
            var scaledSideSize = ApplyHorizontalLayout(tips, sideScale);
            __instance.Size = scaledSideSize;
            __instance.GlobalPosition = ClampToViewport(
                GetSidePosition(globalStartLocation, alignment, scaledSideSize),
                scaledSideSize,
                viewportSize);
            return false;
        }

        // Large in-hand prediction sets are more readable above the source card than heavily shrunk on the side.
        var scale = Mathf.Min(1f, (viewportSize.X - ViewportMargin * 2f) / naturalSize.X);
        var scaledSize = ApplyHorizontalLayout(tips, scale);
        __instance.Size = scaledSize;
        __instance.GlobalPosition = GetTopPosition(
            sourceRect.Rect,
            scaledSize,
            viewportSize);

        return false;
    }

    private static Vector2 ApplyHorizontalLayout(IReadOnlyList<Control> tips, float scale)
    {
        var size = Vector2.Zero;
        var nextPosition = Vector2.Zero;
        var scaledPadding = Padding * scale;

        foreach (var tip in tips)
        {
            tip.Scale = Vector2.One * scale;
            tip.Position = nextPosition;

            var scaledSize = tip.Size * scale;
            size = new Vector2(
                Mathf.Max(nextPosition.X + scaledSize.X, size.X),
                Mathf.Max(scaledSize.Y, size.Y));
            nextPosition += Vector2.Right * (scaledSize.X + scaledPadding);
        }

        return size;
    }

    private static Vector2 GetSidePosition(
        Vector2 globalStartLocation,
        HoverTipAlignment alignment,
        Vector2 size)
    {
        return alignment switch
        {
            HoverTipAlignment.Left => globalStartLocation + Vector2.Left * size.X,
            _ => globalStartLocation
        };
    }

    private static Vector2 GetTopPosition(
        Rect2 sourceRect,
        Vector2 size,
        Vector2 viewportSize)
    {
        var anchorX = sourceRect.Position.X + sourceRect.Size.X / 2f;
        var topY = sourceRect.Position.Y - size.Y - TopGap;

        return new Vector2(
            Clamp(anchorX - size.X / 2f, ViewportMargin, viewportSize.X - ViewportMargin - size.X),
            Clamp(topY, ViewportMargin, viewportSize.Y - ViewportMargin - size.Y));
    }

    private static Vector2 ClampToViewport(Vector2 position, Vector2 size, Vector2 viewportSize)
    {
        return new Vector2(
            Clamp(position.X, ViewportMargin, viewportSize.X - ViewportMargin - size.X),
            Clamp(position.Y, ViewportMargin, viewportSize.Y - ViewportMargin - size.Y));
    }

    private static bool FitsWithinViewport(Vector2 position, Vector2 size, Vector2 viewportSize)
    {
        return position.X >= ViewportMargin &&
            position.Y >= ViewportMargin &&
            position.X + size.X <= viewportSize.X - ViewportMargin &&
            position.Y + size.Y <= viewportSize.Y - ViewportMargin;
    }

    private static float Clamp(float value, float min, float max)
    {
        return max < min
            ? min
            : Mathf.Clamp(value, min, max);
    }

    private sealed class SourceRect(Rect2 rect)
    {
        public Rect2 Rect { get; } = rect;
    }
}

[HarmonyPatch(typeof(NHoverTipSet), nameof(NHoverTipSet.SetAlignmentForCardHolder))]
internal static class PredictionCardHoverTipSourceRectPatch
{
    private static readonly AccessTools.FieldRef<NHoverTipSet, NHoverTipCardContainer> CardContainerField =
        AccessTools.FieldRefAccess<NHoverTipSet, NHoverTipCardContainer>("_cardHoverTipContainer");

    private static void Prefix(NHoverTipSet __instance, NCardHolder holder)
    {
        var container = CardContainerField(__instance);
        if (container == null)
        {
            return;
        }

        var hasPredictionCard = container
            .GetChildren()
            .OfType<Control>()
            .Any(tip => tip.HasMeta(PredictionCardHoverTipLayoutPatches.PredictionCardMetaKey));
        if (!hasPredictionCard)
        {
            return;
        }

        // LayoutResizeAndReposition only receives a side anchor. Record the hovered card rect so fallback
        // placement can center above the card instead of guessing from the left/right edge.
        PredictionCardHoverTipLayoutPatches.RecordSourceRect(container, holder.Hitbox.GetGlobalRect());
    }
}

[HarmonyPatch(typeof(NHoverTipSet), nameof(NHoverTipSet.SetAlignment))]
internal static class PredictionCardHoverTipControlSourceRectPatch
{
    private static readonly AccessTools.FieldRef<NHoverTipSet, NHoverTipCardContainer> CardContainerField =
        AccessTools.FieldRefAccess<NHoverTipSet, NHoverTipCardContainer>("_cardHoverTipContainer");

    private static void Prefix(NHoverTipSet __instance, Control node)
    {
        var container = CardContainerField(__instance);
        if (container == null)
        {
            return;
        }

        var hasPredictionCard = container
            .GetChildren()
            .OfType<Control>()
            .Any(tip => tip.HasMeta(PredictionCardHoverTipLayoutPatches.PredictionCardMetaKey));
        if (!hasPredictionCard)
        {
            return;
        }

        PredictionCardHoverTipLayoutPatches.RecordSourceRect(container, node.GetGlobalRect());
    }
}
