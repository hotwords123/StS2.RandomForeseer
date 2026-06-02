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

[HarmonyPatch(typeof(NHoverTipCardContainer))]
internal static class PredictionCardHoverTipLayoutPatches
{
    internal static readonly StringName PredictionCardMetaKey = $"{Entry.ModId}_PredictionCard";
    private const float Padding = 4f;
    private const float SideGap = 10f;
    private const float ViewportMargin = 12f;
    private const float TopGap = 12f;
    private const float MinScale = 0.55f;
    private const float BundleCardScale = 1f;
    private const float BundleCardSeparation = 45f;
    private static readonly ConditionalWeakTable<NHoverTipCardContainer, SourceRect> SourceRects = [];

    [HarmonyPatch(nameof(NHoverTipCardContainer.Add))]
    [HarmonyPrefix]
    private static bool AddPredictionCardBundleTip(NHoverTipCardContainer __instance, CardHoverTip cardTip)
    {
        if (cardTip is not PredictionCardBundleHoverTip bundleTip)
        {
            return true;
        }

        var control = CreateBundleTip(bundleTip.Bundles);
        control.SetMeta(PredictionCardMetaKey, Variant.From(true));
        __instance.AddChildSafely(control);
        RefreshBundleCards(control);
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
        var availableWidth = viewportSize.X - ViewportMargin * 2f;
        var naturalSize = ApplyWrappedLayout(tips, scale: 1f, rows: 1);
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
            var sideLayout = GetBestWrappedLayout(tips, availableWidth);
            var scaledSideSize = ApplyWrappedLayout(tips, sideLayout.Scale, sideLayout.Rows);
            __instance.Size = scaledSideSize;
            __instance.GlobalPosition = ClampToViewport(
                GetSidePosition(globalStartLocation, alignment, scaledSideSize),
                scaledSideSize,
                viewportSize);
            return false;
        }

        // Large in-hand prediction sets are more readable above the source card than heavily shrunk on the side.
        var layout = GetBestWrappedLayout(tips, availableWidth);
        var scaledSize = ApplyWrappedLayout(tips, layout.Scale, layout.Rows);
        __instance.Size = scaledSize;
        __instance.GlobalPosition = GetTopPosition(
            sourceRect.Rect,
            scaledSize,
            viewportSize);

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

    private static Control CreateBundleTip(IReadOnlyList<IReadOnlyList<CardModel>> bundles)
    {
        var root = new Control
        {
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        var nextX = 0f;
        var stacks = bundles.Select(CreateStack).ToList();
        var height = stacks
            .Select(stack => stack.Size.Y)
            .DefaultIfEmpty(0f)
            .Max();

        foreach (var stack in stacks)
        {
            stack.Position = new Vector2(nextX, height - stack.Size.Y);
            root.AddChildSafely(stack);
            nextX += stack.Size.X + Padding;
        }

        root.Size = new Vector2(Mathf.Max(0f, nextX - Padding), height);
        return root;
    }

    private static Control CreateStack(IReadOnlyList<CardModel> cards)
    {
        var stack = new Control
        {
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        var size = Vector2.Zero;

        for (var i = 0; i < cards.Count; i++)
        {
            var cardNode = CreateCardTipControl(cards[i]);
            var offset = new Vector2(-1f, 1f) * BundleCardSeparation * (i - cards.Count / 2f) * BundleCardScale;
            cardNode.Scale = Vector2.One * BundleCardScale;
            cardNode.Position = offset;

            var brightness = cards.Count <= 1
                ? 1f
                : 0.5f + i / (float)(cards.Count - 1) * 0.5f;
            cardNode.Modulate = new Color(brightness, brightness, brightness);

            stack.AddChildSafely(cardNode);
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

    private static Control CreateCardTipControl(CardModel card)
    {
#pragma warning disable RITSU013
        var scenePath = "res://scenes/ui/" + "card_hover_tip.tscn";
        var control = PreloadManager.Cache.GetScene(scenePath)
            .Instantiate<Control>(PackedScene.GenEditState.Disabled);
#pragma warning restore RITSU013
        var node = control.GetNode<NCard>("%Card");
        node.Model = card;
        node.UpdateVisuals(PileType.Deck, CardPreviewMode.Normal);
        SaveManager.Instance.MarkCardAsSeen(card);
        return control;
    }

    private static void RefreshBundleCards(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is NCard card)
            {
                card.UpdateVisuals(PileType.Deck, CardPreviewMode.Normal);
            }

            RefreshBundleCards(child);
        }
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

    private readonly record struct WrappedLayout(int Rows, float Scale);
}

[HarmonyPatch(typeof(NHoverTipSet), nameof(NHoverTipSet.SetAlignmentForCardHolder))]
internal static class PredictionCardHoverTipSourceRectPatch
{
    private static void Prefix(NHoverTipSet __instance, NCardHolder holder)
    {
        var container = __instance._cardHoverTipContainer;
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
    private static void Prefix(NHoverTipSet __instance, Control node)
    {
        var container = __instance._cardHoverTipContainer;
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
