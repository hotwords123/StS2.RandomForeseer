using Godot;

namespace RandomForeseer.Common.HoverTips;

/// <summary>
/// Stores the UI metadata needed to recognize prediction hover tip nodes.
/// </summary>
internal static class PredictionHoverTipMeta
{
    private static readonly StringName PredictionTextTipMeta = $"{Entry.ModId}_PredictionText";
    private static readonly StringName PredictionCardTipMeta = $"{Entry.ModId}_PredictionCard";

    /// <summary>
    /// Marks a text hover tip control as a prediction text hover tip.
    /// </summary>
    public static void MarkPredictionText(Node node) => node.SetMeta(PredictionTextTipMeta, Variant.From(true));

    /// <summary>
    /// Returns whether a text hover tip control is a prediction text hover tip.
    /// </summary>
    public static bool IsPredictionText(Node node) => node.HasMeta(PredictionTextTipMeta);

    /// <summary>
    /// Marks a card or bundle root so the container layout patch knows that custom prediction layout is required.
    /// </summary>
    public static void MarkPredictionCard(Node node) => node.SetMeta(PredictionCardTipMeta, Variant.From(true));

    /// <summary>
    /// Returns whether a top-level card-container child participates in prediction layout.
    /// </summary>
    public static bool IsPredictionCard(Node node) => node.HasMeta(PredictionCardTipMeta);
}
