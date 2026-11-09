using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Godot;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.HoverTips;

namespace RandomForeseer.Common.HoverTips;

/// <summary>
/// Stores the UI metadata needed to recognize prediction card controls and position their fallback layout.
/// </summary>
internal static class PredictionCardHoverTipLayoutState
{
    internal static readonly StringName PredictionCardMetaKey = $"{Entry.ModId}_PredictionCard";
    private static readonly ConditionalWeakTable<NHoverTipCardContainer, PredictionCardHoverTipSourceRect> SourceRects = [];

    /// <summary>
    /// Marks a card or bundle root so the container layout patch knows that custom prediction layout is required.
    /// </summary>
    public static void MarkPredictionCard(Control? control)
    {
        control?.SetMeta(PredictionCardMetaKey, Variant.From(true));
    }

    /// <summary>
    /// Returns whether a top-level card-container child participates in prediction layout.
    /// </summary>
    public static bool IsPredictionCard(Control control)
    {
        return control.HasMeta(PredictionCardMetaKey);
    }

    /// <summary>
    /// Returns whether the container has at least one top-level prediction card or bundle control.
    /// </summary>
    public static bool HasPredictionCard(NHoverTipCardContainer container)
    {
        return container
            .GetChildren()
            .OfType<Control>()
            .Any(IsPredictionCard);
    }

    /// <summary>
    /// Records the hovered object's bounds and alignment before vanilla positioning runs.
    /// </summary>
    /// <remarks>
    /// <see cref="NHoverTipCardContainer.LayoutResizeAndReposition"/> receives only a side anchor, which is
    /// insufficient for a centered top/bottom fallback. Alignment prefixes must call this only after prediction card
    /// controls have been created. The weak-table entry follows the container lifetime.
    /// </remarks>
    public static void RecordSourceRect(
        NHoverTipCardContainer container,
        Rect2 sourceRect,
        HoverTipAlignment alignment = HoverTipAlignment.None,
        float textGap = 0f)
    {
        SourceRects.AddOrUpdate(container, new(sourceRect, alignment, textGap));
    }

    /// <summary>
    /// Retrieves source geometry previously recorded for this card container.
    /// </summary>
    public static bool TryGetSourceRect(
        NHoverTipCardContainer container,
        [NotNullWhen(true)] out PredictionCardHoverTipSourceRect? sourceRect)
    {
        return SourceRects.TryGetValue(container, out sourceRect);
    }
}

/// <summary>
/// Captures source geometry and spacing needed to reconstruct layout after vanilla alignment.
/// </summary>
internal sealed record PredictionCardHoverTipSourceRect(
    Rect2 Rect,
    HoverTipAlignment Alignment,
    float TextGap);

/// <summary>
/// Sizes and positions mixed prediction card and bundle controls while preserving vanilla alignment when it fits.
/// </summary>
internal static class PredictionCardHoverTipLayout
{
    private const float Padding = 4f;
    private const float SideGap = 10f;
    private const float ViewportMargin = 12f;
    private const float TopGap = 12f;
    private const float MinScale = 0.55f;
    internal const float CardHolderTextGap = 10f;

    /// <summary>
    /// Attempts to apply prediction-aware sizing, wrapping and provisional side placement to a card container.
    /// </summary>
    /// <remarks>
    /// This method runs from a prefix on vanilla card-container layout. It returns <see langword="true"/> only when
    /// it has fully handled that method and the original should be skipped. When source geometry is available, this
    /// stage deliberately leaves final top/bottom fallback to <see cref="ApplyFallbackLayoutIfStillOverflowing"/>,
    /// which runs after vanilla has attempted to align the complete text-and-card HoverTip set.
    /// </remarks>
    /// <returns><see langword="true"/> when custom layout was applied; otherwise <see langword="false"/>.</returns>
    public static bool TryLayoutPredictionCardTips(
        NHoverTipCardContainer container,
        Vector2 globalStartLocation,
        HoverTipAlignment alignment)
    {
        var tips = container.GetChildren().OfType<Control>().ToList();
        if (!tips.Any(PredictionCardHoverTipLayoutState.IsPredictionCard))
        {
            return false;
        }

        var game = NGame.Instance;
        if (game == null)
        {
            return false;
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
            return true;
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
            return true;
        }

        // Do not choose the mod's top/bottom fallback yet. The NHoverTipSet postfix below runs after vanilla's
        // CorrectHorizontalOverflow, so vertical fallback is reserved for cases vanilla still cannot keep visible.
        container.GlobalPosition = GetSidePosition(globalStartLocation, alignment, scaledSize);

        return true;
    }

    /// <summary>
    /// Chooses the smallest row count and largest useful scale that can fit the available viewport width.
    /// </summary>
    /// <remarks>
    /// Multiple tips prefer one row and do not scale below <see cref="MinScale"/> until additional rows are tried.
    /// A single oversized tip has no wrapping alternative, so it may scale below that threshold.
    /// </remarks>
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

    /// <summary>
    /// Applies scale and row-major positions to the controls and returns their resulting bounding size.
    /// </summary>
    /// <remarks>
    /// Controls in each row are bottom-aligned so ordinary cards and differently sized bundle stacks can be mixed.
    /// Unlike <see cref="GetWrappedLayoutSize"/>, this method mutates every supplied control.
    /// </remarks>
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

    /// <summary>
    /// Measures the same row-major layout produced by <see cref="ApplyWrappedLayout"/> without mutating controls.
    /// </summary>
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

    /// <summary>
    /// Converts vanilla's side anchor into the prediction container's top-left position, including the extra gap.
    /// </summary>
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

    /// <summary>
    /// Centers a container above its source when possible, otherwise below it, then clamps it to the viewport.
    /// </summary>
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

    /// <summary>
    /// Clamps both axes to the viewport margin; oversized content is pinned to the leading margin.
    /// </summary>
    private static Vector2 ClampToViewport(Vector2 position, Vector2 size, Vector2 viewportSize)
    {
        return new Vector2(
            Clamp(position.X, ViewportMargin, viewportSize.X - ViewportMargin - size.X),
            Clamp(position.Y, ViewportMargin, viewportSize.Y - ViewportMargin - size.Y));
    }

    /// <summary>
    /// Returns whether a proposed container rectangle fits inside the viewport margin on both axes.
    /// </summary>
    private static bool FitsWithinViewport(Vector2 position, Vector2 size, Vector2 viewportSize)
    {
        return position.X >= ViewportMargin &&
            position.Y >= ViewportMargin &&
            position.X + size.X <= viewportSize.X - ViewportMargin &&
            position.Y + size.Y <= viewportSize.Y - ViewportMargin;
    }

    /// <summary>
    /// Measures a candidate by its worst single-axis overflow beyond the configured viewport margin.
    /// </summary>
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

    /// <summary>
    /// Clamps only the horizontal coordinate, preserving a candidate's vertical fallback position.
    /// </summary>
    private static Vector2 ClampHorizontalToViewport(Vector2 position, Vector2 size, Vector2 viewportSize)
    {
        return new Vector2(
            Clamp(position.X, ViewportMargin, viewportSize.X - ViewportMargin - size.X),
            position.Y);
    }

    /// <summary>
    /// Tests horizontal visibility without applying the prediction viewport margin used during initial placement.
    /// </summary>
    private static bool FitsHorizontallyWithinViewport(Rect2 rect, Vector2 viewportSize)
    {
        return rect.Position.X >= 0f && rect.End.X <= viewportSize.X;
    }

    /// <summary>
    /// Clamps a coordinate while handling content larger than the available interval by pinning it to <paramref name="min"/>.
    /// </summary>
    private static float Clamp(float value, float min, float max)
    {
        return max < min
            ? min
            : Mathf.Clamp(value, min, max);
    }

    /// <summary>
    /// Applies prediction top/bottom fallback only when vanilla alignment still leaves cards or the combined set off-screen.
    /// </summary>
    /// <remarks>
    /// Call this from an <see cref="NHoverTipSet"/> alignment postfix, after vanilla has positioned and horizontally corrected
    /// both containers. It is a no-op without recorded source geometry or prediction card controls.
    /// </remarks>
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

        // This is called from NHoverTipSet alignment postfixes, after vanilla has already had
        // a chance to place both containers. Vanilla also clamps vertical overflow before its horizontal fallback,
        // so reserve the prediction top/bottom fallback for cases that still cannot fit horizontally.
        if (FitsHorizontallyWithinViewport(cardRect, viewportSize) &&
            FitsHorizontallyWithinViewport(combinedRect, viewportSize))
        {
            return;
        }

        ApplyFallbackLayout(tipSet, sourceRect, viewportSize);
    }

    /// <summary>
    /// Repositions text near its requested side and cards above or below the source, resolving overlap when needed.
    /// </summary>
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

    /// <summary>
    /// Reconstructs the requested left/right text position from the original source rect and clamps it on-screen.
    /// </summary>
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

    /// <summary>
    /// Computes the normal centered vertical card fallback before text-overlap resolution is considered.
    /// </summary>
    private static Vector2 GetFallbackCardPosition(
        Vector2 cardSize,
        Rect2 sourceRect,
        Vector2 viewportSize)
    {
        return GetVerticalFallbackPosition(sourceRect, cardSize, viewportSize);
    }

    /// <summary>
    /// Selects a card position that avoids the text container while minimizing viewport overflow and source distance.
    /// </summary>
    /// <remarks>
    /// Candidate ordering first minimizes overflow, then prefers placement above the source, then the shortest distance
    /// back to the source. Horizontal candidates are placed on the side opposite the text.
    /// </remarks>
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

    /// <summary>
    /// Describes the row count and uniform scale selected for a wrapped card-tip layout.
    /// </summary>
    private readonly record struct WrappedLayout(int Rows, float Scale);
}
