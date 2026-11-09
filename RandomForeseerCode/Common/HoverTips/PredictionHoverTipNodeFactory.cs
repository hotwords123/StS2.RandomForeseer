using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.HoverTips;

namespace RandomForeseer.Common.HoverTips;

/// <summary>
/// Builds Godot controls for prediction card bundles without invoking vanilla HoverTip card creation.
/// </summary>
internal static class PredictionHoverTipControlFactory
{
    private const string CardHoverTipScenePath = "res://scenes/ui/card_hover_tip.tscn";
    private const float BundleCardScale = 1f;
    private const float BundleCardSeparation = 45f;

    /// <summary>
    /// Adds one bundle stack control to the card container and lays out its cards.
    /// </summary>
    /// <remarks>
    /// The bundle must be non-empty, as guaranteed by <see cref="PredictionHoverTipFactory.CardBundle"/>. The
    /// returned control is marked as a prediction card by the caller; callers must retain that marker so the custom
    /// prediction layout and fallback positioning are selected.
    /// </remarks>
    public static Control CreateAndAddStack(NHoverTipCardContainer parent, PredictionCardBundleHoverTip bundleTip)
    {
        var stack = new Control
        {
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        parent.AddChildSafely(stack);

        var size = Vector2.Zero;
        var cards = bundleTip.Kind is PredictionCardBundleKind.ScrollBoxes
            ? bundleTip.Cards
            : [.. bundleTip.Cards.Reverse()];

        for (var i = 0; i < cards.Count; i++)
        {
            var cardNode = CreateAndAddCard(stack, cards[i]);
            var centerIndex = (cards.Count - 1) / 2f;
            var offset = new Vector2(-1f, 1f) * BundleCardSeparation * (i - centerIndex) * BundleCardScale;
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

    /// <summary>
    /// Instantiates one card HoverTip scene, assigns its prediction model, and adds it to the supplied stack.
    /// </summary>
    /// <remarks>
    /// The caller owns the stack layout and must provide a card model that is already in the desired visual order.
    /// </remarks>
    private static Control CreateAndAddCard(Control parent, CardModel card)
    {
#pragma warning disable RITSU013
        var control = PreloadManager.Cache.GetScene(CardHoverTipScenePath).Instantiate<Control>();
#pragma warning restore RITSU013
        parent.AddChildSafely(control);

        var node = control.GetNode<NCard>("%Card");
        node.Model = card;
        node.UpdateVisuals(PileType.Deck, CardPreviewMode.Normal);
        return control;
    }
}
