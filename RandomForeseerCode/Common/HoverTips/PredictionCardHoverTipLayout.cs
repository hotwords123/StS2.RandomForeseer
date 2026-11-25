using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Godot;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.HoverTips;

namespace RandomForeseer.Common.HoverTips;

/// <summary>
/// Stores the source geometry and alignment of a hovered object for prediction card layout.
/// </summary>
internal static class PredictionCardHoverTipLayoutState
{
    private static readonly ConditionalWeakTable<NHoverTipCardContainer, PredictionCardHoverTipContext> Contexts = [];

    /// <summary>
    /// Records the hovered object's bounds and alignment before vanilla positioning runs.
    /// </summary>
    /// <remarks>
    /// <see cref="NHoverTipCardContainer.LayoutResizeAndReposition"/> receives only a side anchor, which is
    /// insufficient for a centered top/bottom fallback. Alignment prefixes must call this only after prediction card
    /// controls have been created. The weak-table entry follows the container lifetime.
    /// </remarks>
    public static void RecordContext(
        NHoverTipCardContainer container,
        Rect2 sourceRect,
        HoverTipAlignment alignment = HoverTipAlignment.None,
        float textGap = 0f)
    {
        Contexts.AddOrUpdate(container, new PredictionCardHoverTipContext(sourceRect, alignment, textGap));
    }

    /// <summary>
    /// Retrieves source geometry previously recorded for this card container.
    /// </summary>
    public static bool TryGetContext(
        NHoverTipCardContainer container,
        [NotNullWhen(true)] out PredictionCardHoverTipContext? context)
    {
        return Contexts.TryGetValue(container, out context);
    }
}

/// <summary>
/// Captures source geometry and spacing needed to reconstruct layout after vanilla alignment.
/// </summary>
internal sealed record PredictionCardHoverTipContext(
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
    private const float MinTextHeightRatio = 0.3f;
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
        if (!tips.Any(PredictionHoverTipMeta.IsPredictionCard))
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
        var naturalSize = ComputeWrappedLayout(tips, scale: 1f, rows: 1, applyLayout: true);
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
        var scaledSize = ComputeWrappedLayout(tips, layout.Scale, layout.Rows, applyLayout: true);
        container.Size = scaledSize;
        if (!PredictionCardHoverTipLayoutState.TryGetContext(container, out _))
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
    /// Follows the owner while keeping the relative positions chosen for the prediction cards and text.
    /// </summary>
    public static bool TryFollowOwner(NHoverTipSet tipSet)
    {
        var owner = tipSet._owner;
        if (!tipSet._followOwner || !GodotObject.IsInstanceValid(owner) ||
            NGame.Instance is not { } game ||
            !PredictionCardHoverTipLayoutState.TryGetContext(tipSet._cardHoverTipContainer, out _))
        {
            return false;
        }

        var cardRect = tipSet._cardHoverTipContainer.GetGlobalRect();
        var textContainer = tipSet._textHoverTipContainer;
        var bounds = textContainer.GetChildCount() > 0 ? cardRect.Merge(textContainer.GetGlobalRect()) : cardRect;
        var boundsOffset = bounds.Position - tipSet.GlobalPosition;
        var followPosition = owner.GlobalPosition - tipSet._followOffset + tipSet._extraOffset;
        tipSet.GlobalPosition = ClampToViewport(
            followPosition + boundsOffset,
            bounds.Size,
            game.GetViewportRect().Size,
            margin: 0f) - boundsOffset;

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
        var naturalWidth = ComputeWrappedLayout(tips, scale: 1f, rows: 1, applyLayout: false).X;
        if (naturalWidth <= availableWidth)
        {
            return new WrappedLayout(1, 1f);
        }

        if (tips.Count == 1 || naturalWidth * MinScale <= availableWidth)
        {
            return new WrappedLayout(1, availableWidth / naturalWidth);
        }

        for (var rows = 2; rows <= tips.Count; rows++)
        {
            var rowNaturalWidth = ComputeWrappedLayout(tips, scale: 1f, rows, applyLayout: false).X;
            if (rowNaturalWidth * MinScale <= availableWidth)
            {
                return new WrappedLayout(rows, Mathf.Min(1f, availableWidth / rowNaturalWidth));
            }
        }

        return new WrappedLayout(tips.Count, MinScale);
    }

    /// <summary>
    /// Computes row-major bounds and optionally applies scale and positions to the controls.
    /// </summary>
    /// <remarks>
    /// Controls in each row are bottom-aligned so ordinary cards and differently sized bundle stacks can be mixed.
    /// </remarks>
    private static Vector2 ComputeWrappedLayout(IReadOnlyList<Control> tips, float scale, int rows, bool applyLayout)
    {
        var rowCount = Mathf.Max(1, rows);
        var perRow = Mathf.CeilToInt(tips.Count / (float)rowCount);
        var actualRows = Mathf.CeilToInt(tips.Count / (float)perRow);

        var scaledPadding = Padding * scale;
        var width = 0f;
        var height = 0f;

        for (var row = 0; row < actualRows; row++)
        {
            height += row > 0 ? scaledPadding : 0f;

            var rowStart = row * perRow;
            var rowEnd = Math.Min(rowStart + perRow, tips.Count);
            var rowWidth = 0f;
            var rowHeight = 0f;

            for (var i = rowStart; i < rowEnd; i++)
            {
                rowHeight = Mathf.Max(rowHeight, tips[i].Size.Y * scale);
            }

            for (var i = rowStart; i < rowEnd; i++)
            {
                rowWidth += i > rowStart ? scaledPadding : 0f;

                var tip = tips[i];
                var scaledSize = tip.Size * scale;
                if (applyLayout)
                {
                    tip.Scale = Vector2.One * scale;
                    tip.Position = new Vector2(rowWidth, height + rowHeight - scaledSize.Y);
                }

                rowWidth += scaledSize.X;
            }

            width = Mathf.Max(width, rowWidth);
            height += rowHeight;
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
    private static Vector2 ClampToViewport(
        Vector2 position,
        Vector2 size,
        Vector2 viewportSize,
        float margin = ViewportMargin)
    {
        return new Vector2(
            Clamp(position.X, margin, viewportSize.X - margin - size.X),
            Clamp(position.Y, margin, viewportSize.Y - margin - size.Y));
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
        if (!PredictionCardHoverTipLayoutState.TryGetContext(cardContainer, out var context))
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

        if (hasTextTips)
        {
            LimitFallbackTextHeight(textContainer, context.Rect, viewportSize);
        }

        ApplyFallbackLayout(tipSet, context, viewportSize);
    }

    /// <summary>
    /// Wraps text within the smaller region below the source's top or above its bottom, with a soft height floor.
    /// </summary>
    private static void LimitFallbackTextHeight(VFlowContainer textContainer, Rect2 sourceRect, Vector2 viewportSize)
    {
        var availableBelow = viewportSize.Y - ViewportMargin - sourceRect.Position.Y;
        var availableAbove = sourceRect.End.Y - ViewportMargin;
        var heightLimit = Mathf.Max(viewportSize.Y * MinTextHeightRatio, Mathf.Min(availableBelow, availableAbove));
        var height = Mathf.Max(heightLimit, textContainer.GetCombinedMinimumSize().Y);
        if (textContainer.Size.Y <= height)
        {
            return;
        }

        textContainer.Size = textContainer.Size with { Y = height };

        // Sort now to measure the wrapped width before fallback placement; subsequent layout is automatic.
        textContainer.Notification((int)Container.NotificationSortChildren);
        textContainer.Size = new Vector2(textContainer.GetCombinedMinimumSize().X, height);
    }

    /// <summary>
    /// Repositions text near its requested side and cards above or below the source, resolving overlap when needed.
    /// </summary>
    private static void ApplyFallbackLayout(
        NHoverTipSet tipSet,
        PredictionCardHoverTipContext context,
        Vector2 viewportSize)
    {
        var cardContainer = tipSet._cardHoverTipContainer;
        var textContainer = tipSet._textHoverTipContainer;
        var hasTextTips = textContainer.GetChildren().OfType<Control>().Any();

        if (hasTextTips)
        {
            textContainer.GlobalPosition = GetFallbackTextPosition(textContainer, context, viewportSize);
        }

        cardContainer.GlobalPosition = GetFallbackCardPosition(cardContainer.Size, context.Rect, viewportSize);

        if (hasTextTips && cardContainer.GetGlobalRect().Intersects(textContainer.GetGlobalRect()))
        {
            cardContainer.GlobalPosition = GetFallbackCardPositionAvoidingText(
                cardContainer.Size,
                context.Rect,
                textContainer.GetGlobalRect(),
                viewportSize);
        }
    }

    /// <summary>
    /// Preserves corrected below-source text placement or reconstructs its requested side, then clamps it on-screen.
    /// </summary>
    private static Vector2 GetFallbackTextPosition(
        Control textContainer,
        PredictionCardHoverTipContext context,
        Vector2 viewportSize)
    {
        var textSize = textContainer.Size;
        var (rect, alignment, textGap) = context;

        var position = alignment switch
        {
            HoverTipAlignment.Left => new Vector2(rect.Position.X - textSize.X - textGap, rect.Position.Y),
            HoverTipAlignment.Right => new Vector2(rect.End.X + textGap, rect.Position.Y),
            _ => textContainer.GlobalPosition
        };
        return ClampToViewport(position, textSize, viewportSize);
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
