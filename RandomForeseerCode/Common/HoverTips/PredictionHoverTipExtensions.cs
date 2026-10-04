using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace RandomForeseer.RandomForeseerCode.Common.HoverTips;

/// <summary>
/// Provides lazy collection conversions and common operations for prediction HoverTips.
/// </summary>
/// <remarks>
/// Conversion methods create HoverTips during enumeration. Callers that require stable tip instances or repeated
/// enumeration must materialize the result themselves.
/// </remarks>
internal static class PredictionHoverTipExtensions
{
    /// <summary>
    /// Lazily converts cards to individual prediction card tips while preserving input order.
    /// </summary>
    public static IEnumerable<IHoverTip> ToPredictionHoverTips(this IEnumerable<CardModel> cards)
    {
        return cards.Select(PredictionHoverTipFactory.Card);
    }

    /// <summary>
    /// Lazily converts potions to prediction tips while preserving input order.
    /// </summary>
    public static IEnumerable<IHoverTip> ToPredictionHoverTips(this IEnumerable<PotionModel> potions)
    {
        return potions.Select(PredictionHoverTipFactory.Potion);
    }

    /// <summary>
    /// Lazily converts relics to prediction tips while preserving input order.
    /// </summary>
    public static IEnumerable<IHoverTip> ToPredictionHoverTips(this IEnumerable<RelicModel> relics)
    {
        return relics.Select(PredictionHoverTipFactory.Relic);
    }

    /// <summary>
    /// Lazily converts orbs to prediction tips while preserving input order.
    /// </summary>
    public static IEnumerable<IHoverTip> ToPredictionHoverTips(this IEnumerable<OrbModel> orbs)
    {
        return orbs.Select(PredictionHoverTipFactory.Orb);
    }

    /// <summary>
    /// Lazily converts each non-empty card bundle to one logical prediction bundle tip.
    /// </summary>
    /// <remarks>
    /// Empty bundles are ignored. Callers must pass cards in prediction/semantic order and use <paramref name="kind"/>
    /// to describe presentation semantics; whether bundles expand or remain stacks is decided later from the complete
    /// HoverTip set.
    /// </remarks>
    public static IEnumerable<IHoverTip> ToPredictionCardBundleHoverTips(
        this IEnumerable<IReadOnlyList<CardModel>> bundles,
        PredictionCardBundleKind kind = PredictionCardBundleKind.Regular)
    {
        return bundles
            .Where(cards => cards.Count > 0)
            .Select(cards => PredictionHoverTipFactory.CardBundle(cards, kind));
    }

    /// <summary>
    /// Returns whether a tip is any prediction-owned text/model or card tip.
    /// </summary>
    public static bool IsPredictionHoverTip(this IHoverTip tip)
    {
        return IsPredictionTextHoverTip(tip) || IsPredictionCardHoverTip(tip);
    }

    /// <summary>
    /// Returns whether a tip belongs in the text-tip container and carries the prediction ID prefix.
    /// </summary>
    public static bool IsPredictionTextHoverTip(this IHoverTip tip)
    {
        return !string.IsNullOrEmpty(tip.Id) &&
               tip.Id.StartsWith(PredictionHoverTipFactory.HoverTipIdPrefix, StringComparison.Ordinal);
    }

    /// <summary>
    /// Returns whether a tip is an individual prediction card or logical prediction bundle.
    /// </summary>
    public static bool IsPredictionCardHoverTip(this IHoverTip tip)
    {
        return tip is PredictionCardHoverTip or PredictionCardBundleHoverTip;
    }
}
