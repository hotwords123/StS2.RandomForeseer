using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Saves;

namespace RandomForeseer.Common;

internal static class PredictionCardHoverTipLayoutState
{
    internal static readonly StringName PredictionCardMetaKey = $"{Entry.ModId}_PredictionCard";
    private static readonly ConditionalWeakTable<NHoverTipCardContainer, PredictionCardHoverTipSourceRect> SourceRects = [];

    public static void MarkPredictionCard(Control? control)
    {
        control?.SetMeta(PredictionCardMetaKey, Variant.From(true));
    }

    public static bool IsPredictionCard(Control control)
    {
        return control.HasMeta(PredictionCardMetaKey);
    }

    public static bool HasPredictionCard(NHoverTipCardContainer container)
    {
        return container
            .GetChildren()
            .OfType<Control>()
            .Any(IsPredictionCard);
    }

    public static void RecordSourceRect(
        NHoverTipCardContainer container,
        Rect2 sourceRect,
        HoverTipAlignment alignment = HoverTipAlignment.None,
        float textGap = 0f)
    {
        SourceRects.Remove(container);
        SourceRects.Add(container, new PredictionCardHoverTipSourceRect(sourceRect, alignment, textGap));
    }

    public static bool TryGetSourceRect(
        NHoverTipCardContainer container,
        [NotNullWhen(true)] out PredictionCardHoverTipSourceRect? sourceRect)
    {
        return SourceRects.TryGetValue(container, out sourceRect);
    }
}

internal sealed class PredictionCardHoverTipSourceRect(
    Rect2 rect,
    HoverTipAlignment alignment,
    float textGap)
{
    public Rect2 Rect { get; } = rect;

    public HoverTipAlignment Alignment { get; } = alignment;

    public float TextGap { get; } = textGap;
}

internal static class PredictionCardHoverTipBundleFactory
{
    private const float Padding = 4f;
    private const float BundleCardScale = 1f;
    private const float BundleCardSeparation = 45f;

    public static Control CreateAndAddBundleTip(
        NHoverTipCardContainer parent,
        IReadOnlyList<IReadOnlyList<CardModel>> bundles)
    {
        var root = new Control
        {
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        parent.AddChildSafely(root);

        var nextX = 0f;
        var stacks = bundles
            .Select(bundle => CreateAndAddStack(root, bundle))
            .ToList();
        var height = stacks
            .Select(stack => stack.Size.Y)
            .DefaultIfEmpty(0f)
            .Max();

        foreach (var stack in stacks)
        {
            stack.Position = new Vector2(nextX, height - stack.Size.Y);
            nextX += stack.Size.X + Padding;
        }

        root.Size = new Vector2(Mathf.Max(0f, nextX - Padding), height);
        return root;
    }

    private static Control CreateAndAddStack(Control parent, IReadOnlyList<CardModel> cards)
    {
        var stack = new Control
        {
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        parent.AddChildSafely(stack);

        var size = Vector2.Zero;

        for (var i = 0; i < cards.Count; i++)
        {
            var cardNode = CreateAndAddCardTipControl(stack, cards[i]);
            var offset = new Vector2(-1f, 1f) * BundleCardSeparation * (i - cards.Count / 2f) * BundleCardScale;
            cardNode.Scale = Vector2.One * BundleCardScale;
            cardNode.Position = offset;

            var brightness = cards.Count <= 1
                ? 1f
                : 0.5f + i / (float)(cards.Count - 1) * 0.5f;
            cardNode.Modulate = new Color(brightness, brightness, brightness);

            size = new Vector2(
                Mathf.Max(size.X, offset.X + cardNode.Size.X * BundleCardScale),
                Mathf.Max(size.Y, offset.Y + cardNode.Size.Y * BundleCardScale));
        }

        var children = stack.GetChildren().OfType<Control>().ToList();
        var minX = children
            .OfType<Control>()
            .Select(child => child.Position.X)
            .DefaultIfEmpty(0f)
            .Min();
        var minY = children
            .Select(child => child.Position.Y)
            .DefaultIfEmpty(0f)
            .Min();
        if (minX < 0f || minY < 0f)
        {
            var adjustment = new Vector2(
                minX < 0f ? -minX : 0f,
                minY < 0f ? -minY : 0f);
            foreach (var child in children)
            {
                child.Position += adjustment;
            }

            size += adjustment;
        }

        stack.Size = size;
        return stack;
    }

    private static Control CreateAndAddCardTipControl(Control parent, CardModel card)
    {
#pragma warning disable RITSU013
        var scenePath = "res://scenes/ui/" + "card_hover_tip.tscn";
        var control = PreloadManager.Cache.GetScene(scenePath)
            .Instantiate<Control>(PackedScene.GenEditState.Disabled);
#pragma warning restore RITSU013
        parent.AddChildSafely(control);

        var node = control.GetNode<NCard>("%Card");
        node.Model = card;
        node.UpdateVisuals(PileType.Deck, CardPreviewMode.Normal);
        SaveManager.Instance.MarkCardAsSeen(card);
        return control;
    }
}

internal static class PredictionCardHoverTipLayout
{
    private const float Padding = 4f;
    private const float SideGap = 10f;
    private const float ViewportMargin = 12f;
    private const float TopGap = 12f;
    private const float MinScale = 0.55f;
    internal const float CardHolderTextGap = 10f;

    public static bool TryLayoutPredictionCardTips(
        NHoverTipCardContainer container,
        Vector2 globalStartLocation,
        HoverTipAlignment alignment)
    {
        var tips = container.GetChildren().OfType<Control>().ToList();
        if (!tips.Any(PredictionCardHoverTipLayoutState.IsPredictionCard))
        {
            return true;
        }

        var game = NGame.Instance;
        if (game == null)
        {
            return true;
        }

        var viewportSize = game.GetViewportRect().Size;
        var availableWidth = viewportSize.X - ViewportMargin * 2f;
        var naturalSize = ApplyWrappedLayout(tips, scale: 1f, rows: 1);
        var sidePosition = GetSidePosition(globalStartLocation, alignment, naturalSize);

        // Preserve the vanilla side placement when it fits; small prediction sets should behave exactly like before.
        if (FitsWithinViewport(sidePosition, naturalSize, viewportSize))
        {
            container.Size = naturalSize;
            container.GlobalPosition = sidePosition;
            return false;
        }

        // Vanilla positions card tips here, then NHoverTipSet.SetAlignment*/CorrectHorizontalOverflow decides
        // whether card and text tips should stay on opposite sides or move to the same side. Keep this prefix
        // limited to prediction-card sizing/wrapping so the vanilla horizontal fallback still gets the first try.
        var layout = GetBestWrappedLayout(tips, availableWidth);
        var scaledSize = ApplyWrappedLayout(tips, layout.Scale, layout.Rows);
        container.Size = scaledSize;
        if (!PredictionCardHoverTipLayoutState.TryGetSourceRect(container, out _))
        {
            // Without a source rect we cannot place a vertical fallback around the hovered object reliably, so keep
            // the old conservative behavior: fit the prediction cards to the side and clamp them into the viewport.
            container.GlobalPosition = ClampToViewport(
                GetSidePosition(globalStartLocation, alignment, scaledSize),
                scaledSize,
                viewportSize);
            return false;
        }

        // Do not choose the mod's top/bottom fallback yet. The NHoverTipSet postfix below runs after vanilla's
        // CorrectHorizontalOverflow, so vertical fallback is reserved for cases vanilla still cannot keep visible.
        container.GlobalPosition = GetSidePosition(globalStartLocation, alignment, scaledSize);

        return false;
    }

    private static WrappedLayout GetBestWrappedLayout(IReadOnlyList<Control> tips, float availableWidth)
    {
        var naturalWidth = GetWrappedLayoutSize(tips, scale: 1f, rows: 1).X;
        if (naturalWidth <= availableWidth)
        {
            return new WrappedLayout(1, 1f);
        }

        if (tips.Count == 1)
        {
            return new WrappedLayout(1, availableWidth / naturalWidth);
        }

        if (naturalWidth * MinScale <= availableWidth)
        {
            return new WrappedLayout(1, availableWidth / naturalWidth);
        }

        for (var rows = 2; rows <= tips.Count; rows++)
        {
            var rowNaturalWidth = GetWrappedLayoutSize(tips, scale: 1f, rows).X;
            if (rowNaturalWidth * MinScale <= availableWidth)
            {
                return new WrappedLayout(rows, Mathf.Min(1f, availableWidth / rowNaturalWidth));
            }
        }

        return new WrappedLayout(tips.Count, MinScale);
    }

    private static Vector2 ApplyWrappedLayout(IReadOnlyList<Control> tips, float scale, int rows)
    {
        var rowCount = Mathf.Max(1, rows);
        var perRow = Mathf.CeilToInt(tips.Count / (float)rowCount);
        var size = Vector2.Zero;
        var scaledPadding = Padding * scale;

        for (var i = 0; i < tips.Count; i++)
        {
            var tip = tips[i];
            var row = i / perRow;
            var col = i % perRow;
            var rowStart = row * perRow;
            var rowHeight = tips
                .Skip(rowStart)
                .Take(perRow)
                .Select(item => item.Size.Y * scale)
                .DefaultIfEmpty(0f)
                .Max();
            var x = tips
                .Skip(rowStart)
                .Take(col)
                .Sum(item => item.Size.X * scale + scaledPadding);
            var y = 0f;
            for (var previousRow = 0; previousRow < row; previousRow++)
            {
                var previousRowStart = previousRow * perRow;
                y += tips
                    .Skip(previousRowStart)
                    .Take(perRow)
                    .Select(item => item.Size.Y * scale)
                    .DefaultIfEmpty(0f)
                    .Max() + scaledPadding;
            }

            var scaledSize = tip.Size * scale;
            tip.Scale = Vector2.One * scale;
            tip.Position = new Vector2(x, y + rowHeight - scaledSize.Y);

            size = new Vector2(
                Mathf.Max(x + scaledSize.X, size.X),
                Mathf.Max(y + Mathf.Max(scaledSize.Y, rowHeight), size.Y));
        }

        return size;
    }

    private static Vector2 GetWrappedLayoutSize(IReadOnlyList<Control> tips, float scale, int rows)
    {
        var rowCount = Mathf.Max(1, rows);
        var perRow = Mathf.CeilToInt(tips.Count / (float)rowCount);
        var scaledPadding = Padding * scale;
        var width = 0f;
        var height = 0f;

        for (var row = 0; row < rowCount; row++)
        {
            var rowTips = tips
                .Skip(row * perRow)
                .Take(perRow)
                .ToList();
            if (rowTips.Count == 0)
            {
                continue;
            }

            width = Mathf.Max(
                width,
                rowTips.Sum(tip => tip.Size.X * scale) + scaledPadding * (rowTips.Count - 1));
            height += rowTips.Max(tip => tip.Size.Y * scale);
            if (row < rowCount - 1)
            {
                height += scaledPadding;
            }
        }

        return new Vector2(width, height);
    }

    private static Vector2 GetSidePosition(
        Vector2 globalStartLocation,
        HoverTipAlignment alignment,
        Vector2 size)
    {
        return alignment switch
        {
            HoverTipAlignment.Left => globalStartLocation + Vector2.Left * (size.X + SideGap),
            _ => globalStartLocation + Vector2.Right * SideGap
        };
    }

    private static Vector2 GetVerticalFallbackPosition(
        Rect2 sourceRect,
        Vector2 size,
        Vector2 viewportSize)
    {
        var anchorX = sourceRect.Position.X + sourceRect.Size.X / 2f;
        var x = anchorX - size.X / 2f;

        // Top fallback: center above the source when there is enough vertical room.
        var topY = sourceRect.Position.Y - size.Y - TopGap;
        if (topY >= ViewportMargin)
        {
            return ClampToViewport(new Vector2(x, topY), size, viewportSize);
        }

        // Bottom fallback: keep the same horizontal anchor, but place below the source.
        var bottomY = sourceRect.End.Y + TopGap;
        return ClampToViewport(new Vector2(x, bottomY), size, viewportSize);
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

    private static float GetMaxViewportOverflow(Vector2 position, Vector2 size, Vector2 viewportSize)
    {
        var overflowX = MathF.Max(
            ViewportMargin - position.X,
            position.X + size.X - (viewportSize.X - ViewportMargin));
        var overflowY = MathF.Max(
            ViewportMargin - position.Y,
            position.Y + size.Y - (viewportSize.Y - ViewportMargin));
        return MathF.Max(0f, MathF.Max(overflowX, overflowY));
    }

    private static Vector2 ClampHorizontalToViewport(Vector2 position, Vector2 size, Vector2 viewportSize)
    {
        return new Vector2(
            Clamp(position.X, ViewportMargin, viewportSize.X - ViewportMargin - size.X),
            position.Y);
    }

    private static bool FitsHorizontallyWithinViewport(Rect2 rect, Vector2 viewportSize)
    {
        return rect.Position.X >= 0f && rect.End.X <= viewportSize.X;
    }

    private static float Clamp(float value, float min, float max)
    {
        return max < min
            ? min
            : Mathf.Clamp(value, min, max);
    }

    public static void ApplyFallbackLayoutIfStillOverflowing(NHoverTipSet tipSet)
    {
        var cardContainer = tipSet._cardHoverTipContainer;
        if (!PredictionCardHoverTipLayoutState.TryGetSourceRect(cardContainer, out var sourceRect))
        {
            return;
        }

        if (!PredictionCardHoverTipLayoutState.HasPredictionCard(cardContainer))
        {
            return;
        }

        var game = NGame.Instance;
        if (game == null)
        {
            return;
        }

        var viewportSize = game.GetViewportRect().Size;
        var cardRect = cardContainer.GetGlobalRect();
        var textContainer = tipSet._textHoverTipContainer;
        var hasTextTips = textContainer.GetChildren().OfType<Control>().Any();
        var combinedRect = hasTextTips
            ? cardRect.Merge(textContainer.GetGlobalRect())
            : cardRect;

        // This is called from SetAlignment*/SetAlignmentForCardHolder postfixes, after vanilla has already had
        // a chance to place both containers. Vanilla also clamps vertical overflow before its horizontal fallback,
        // so reserve the prediction top/bottom fallback for cases that still cannot fit horizontally.
        if (FitsHorizontallyWithinViewport(cardRect, viewportSize) &&
            FitsHorizontallyWithinViewport(combinedRect, viewportSize))
        {
            return;
        }

        ApplyFallbackLayout(tipSet, sourceRect, viewportSize);
    }

    private static void ApplyFallbackLayout(
        NHoverTipSet tipSet,
        PredictionCardHoverTipSourceRect sourceRect,
        Vector2 viewportSize)
    {
        var cardContainer = tipSet._cardHoverTipContainer;
        var textContainer = tipSet._textHoverTipContainer;
        var hasTextTips = textContainer.GetChildren().OfType<Control>().Any();

        if (hasTextTips)
        {
            textContainer.GlobalPosition = GetFallbackTextPosition(textContainer, sourceRect, viewportSize);
        }

        cardContainer.GlobalPosition = GetFallbackCardPosition(cardContainer.Size, sourceRect.Rect, viewportSize);

        if (hasTextTips && cardContainer.GetGlobalRect().Intersects(textContainer.GetGlobalRect()))
        {
            cardContainer.GlobalPosition = GetFallbackCardPositionAvoidingText(
                cardContainer.Size,
                sourceRect.Rect,
                textContainer.GetGlobalRect(),
                viewportSize);
        }
    }

    private static Vector2 GetFallbackTextPosition(
        Control textContainer,
        PredictionCardHoverTipSourceRect sourceRect,
        Vector2 viewportSize)
    {
        var textSize = textContainer.Size;
        var x = sourceRect.Alignment switch
        {
            HoverTipAlignment.Left => sourceRect.Rect.Position.X - textSize.X - sourceRect.TextGap,
            HoverTipAlignment.Right => sourceRect.Rect.End.X + sourceRect.TextGap,
            _ => textContainer.GlobalPosition.X
        };
        var y = sourceRect.Rect.Position.Y;
        return ClampToViewport(new Vector2(x, y), textSize, viewportSize);
    }

    private static Vector2 GetFallbackCardPosition(
        Vector2 cardSize,
        Rect2 sourceRect,
        Vector2 viewportSize)
    {
        return GetVerticalFallbackPosition(sourceRect, cardSize, viewportSize);
    }

    private static Vector2 GetFallbackCardPositionAvoidingText(
        Vector2 cardSize,
        Rect2 sourceRect,
        Rect2 textRect,
        Vector2 viewportSize)
    {
        var sourceCenter = sourceRect.GetCenter();
        var textCenter = textRect.GetCenter();

        // Keep normal vertical fallback centered on the source.
        var centeredX = sourceCenter.X - cardSize.X / 2f;

        // If possible, avoid the text by moving horizontally to the side opposite the text.
        var horizontalAvoidX = textCenter.X <= sourceCenter.X
            ? textRect.End.X + SideGap
            : textRect.Position.X - cardSize.X - SideGap;

        var sourceAboveY = sourceRect.Position.Y - cardSize.Y - TopGap;
        var sourceBelowY = sourceRect.End.Y + TopGap;

        var textAboveY = textRect.Position.Y - cardSize.Y - TopGap;
        var textBelowY = textRect.End.Y + TopGap;

        var aboveY = Mathf.Min(sourceAboveY, textAboveY);
        var belowY = Mathf.Max(sourceBelowY, textBelowY);

        var candidates = new[]
        {
            ClampHorizontalToViewport(new Vector2(centeredX, aboveY), cardSize, viewportSize),
            ClampHorizontalToViewport(new Vector2(centeredX, belowY), cardSize, viewportSize),
            new Vector2(horizontalAvoidX, sourceAboveY),
            new Vector2(horizontalAvoidX, sourceBelowY)
        };

        return candidates.MinBy(candidate =>
        {
            var overflow = GetMaxViewportOverflow(candidate, cardSize, viewportSize);
            var isAbove = candidate.Y <= sourceCenter.Y;
            var distanceToSource = (candidate + cardSize / 2f).DistanceTo(sourceCenter);
            return (overflow, isAbove ? 0 : 1, distanceToSource);
        });
    }

    private readonly record struct WrappedLayout(int Rows, float Scale);
}

[HarmonyPatch(typeof(NHoverTipCardContainer))]
internal static class PredictionCardHoverTipContainerPatches
{
    [HarmonyPatch(nameof(NHoverTipCardContainer.Add))]
    [HarmonyPrefix]
    private static bool AddPredictionCardBundleTip(NHoverTipCardContainer __instance, CardHoverTip cardTip)
    {
        if (cardTip is not PredictionCardBundleHoverTip bundleTip)
        {
            return true;
        }

        var control = PredictionCardHoverTipBundleFactory.CreateAndAddBundleTip(__instance, bundleTip.Bundles);
        PredictionCardHoverTipLayoutState.MarkPredictionCard(control);
        return false;
    }

    [HarmonyPatch(nameof(NHoverTipCardContainer.Add))]
    [HarmonyPostfix]
    private static void MarkPredictionCardTip(NHoverTipCardContainer __instance, CardHoverTip cardTip)
    {
        if (cardTip is not PredictionCardHoverTip)
        {
            return;
        }

        var control = __instance.GetChildren().OfType<Control>().LastOrDefault();
        PredictionCardHoverTipLayoutState.MarkPredictionCard(control);
    }

    [HarmonyPatch(nameof(NHoverTipCardContainer.LayoutResizeAndReposition))]
    [HarmonyPrefix]
    private static bool LayoutPredictionCardTips(
        NHoverTipCardContainer __instance,
        Vector2 globalStartLocation,
        HoverTipAlignment alignment)
    {
        return PredictionCardHoverTipLayout.TryLayoutPredictionCardTips(
            __instance,
            globalStartLocation,
            alignment);
    }
}

[HarmonyPatch(typeof(NHoverTipSet))]
internal static class PredictionCardHoverTipSetAlignmentPatches
{
    [HarmonyPatch(nameof(NHoverTipSet.SetAlignmentForCardHolder))]
    [HarmonyPrefix]
    private static void RecordCardHolderSourceRect(NHoverTipSet __instance, NCardHolder holder)
    {
        var container = __instance._cardHoverTipContainer;
        if (!ShouldRecordSourceRect(container))
        {
            return;
        }

        // LayoutResizeAndReposition only receives a side anchor. Record the hovered card rect so fallback
        // placement can center above the card instead of guessing from the left/right edge.
        PredictionCardHoverTipLayoutState.RecordSourceRect(
            container,
            holder.Hitbox.GetGlobalRect(),
            HoverTip.GetHoverTipAlignment(holder),
            PredictionCardHoverTipLayout.CardHolderTextGap);
    }

    [HarmonyPatch(nameof(NHoverTipSet.SetAlignmentForCardHolder))]
    [HarmonyPostfix]
    private static void ApplyCardHolderFallbackLayout(NHoverTipSet __instance)
    {
        PredictionCardHoverTipLayout.ApplyFallbackLayoutIfStillOverflowing(__instance);
    }

    [HarmonyPatch(nameof(NHoverTipSet.SetAlignment))]
    [HarmonyPrefix]
    private static void RecordControlSourceRect(NHoverTipSet __instance, Control node, HoverTipAlignment alignment)
    {
        var container = __instance._cardHoverTipContainer;
        if (!ShouldRecordSourceRect(container))
        {
            return;
        }

        PredictionCardHoverTipLayoutState.RecordSourceRect(container, node.GetGlobalRect(), alignment);
    }

    [HarmonyPatch(nameof(NHoverTipSet.SetAlignment))]
    [HarmonyPostfix]
    private static void ApplyControlFallbackLayout(NHoverTipSet __instance)
    {
        PredictionCardHoverTipLayout.ApplyFallbackLayoutIfStillOverflowing(__instance);
    }

    private static bool ShouldRecordSourceRect(NHoverTipCardContainer? container)
    {
        return container != null && PredictionCardHoverTipLayoutState.HasPredictionCard(container);
    }
}
