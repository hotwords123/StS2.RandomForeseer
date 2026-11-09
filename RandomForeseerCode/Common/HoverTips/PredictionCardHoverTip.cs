using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace RandomForeseer.Common.HoverTips;

/// <summary>
/// Displays one predicted card without exposing its canonical model to vanilla discovery tracking.
/// </summary>
internal class PredictionCardHoverTip(CardModel card, bool isDimmed)
    : CardHoverTip(card), IHoverTip
{
    /// <summary>
    /// Whether the card should be visually de-emphasized.
    /// </summary>
    public bool IsDimmed { get; } = isDimmed;

    bool IHoverTip.IsInstanced => true;

    // Hide the canonical card from NHoverTipSet so predicted cards do not mark progress as discovered.
    AbstractModel? IHoverTip.CanonicalModel => null;
}

/// <summary>
/// Represents one non-empty logical card bundle whose final presentation depends on the complete HoverTip set.
/// </summary>
/// <remarks>
/// <see cref="Cards"/> remains in prediction/semantic order. Callers must not reverse it for stack rendering;
/// the layout layer derives visual stacking order from <see cref="Kind"/>. A complete tip set containing exactly
/// one bundle and no independent card tip may expand it into ordinary card tips; otherwise the bundle remains an
/// independently laid-out stack.
/// </remarks>
internal class PredictionCardBundleHoverTip(
    IReadOnlyList<CardModel> cards,
    PredictionCardBundleKind kind,
    bool isDimmed)
    : CardHoverTip(cards[0]), IHoverTip
{
    /// <summary>
    /// Cards in prediction/semantic order. The collection is guaranteed to be non-empty by the factory.
    /// </summary>
    public IReadOnlyList<CardModel> Cards { get; } = cards;

    /// <summary>
    /// Presentation semantics applied when the bundle remains a stack.
    /// </summary>
    public PredictionCardBundleKind Kind { get; } = kind;

    /// <summary>
    /// Whether the entire card bundle should be visually de-emphasized.
    /// </summary>
    public bool IsDimmed { get; } = isDimmed;

    string IHoverTip.Id => string.Empty;

    bool IHoverTip.IsInstanced => true;

    // Hide the canonical card from NHoverTipSet so predicted bundled cards do not mark progress as discovered.
    AbstractModel? IHoverTip.CanonicalModel => null;
}

/// <summary>
/// Describes bundle-specific presentation behavior without changing the cards' semantic order.
/// </summary>
internal enum PredictionCardBundleKind
{
    /// <summary>
    /// A regular prediction bundle. Its visual stack uses the legacy reversed stacking order.
    /// </summary>
    Regular,

    /// <summary>
    /// A Scroll Boxes reward bundle, which uses vanilla card-bundle stacking order.
    /// </summary>
    ScrollBoxes,

    /// <summary>
    /// A transform-result bundle. It uses regular stacking order and participates in the shared transform explanation.
    /// </summary>
    Transform
}
