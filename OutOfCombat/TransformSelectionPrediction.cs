using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using RandomForeseer.Common;

namespace RandomForeseer.OutOfCombat;

internal sealed class TransformSelectionPrediction(
    IReadOnlySet<CardModel> selectedCards,
    Func<CardModel, CardTransformation> cardToTransformation)
{
    public IReadOnlyList<IHoverTip> GetHoverTips(CardModel hoveredCard)
    {
        var replacement = PredictReplacement(hoveredCard);
        if (replacement == null)
        {
            return [];
        }

        var tipKey = selectedCards.Contains(hoveredCard)
            ? "transform_selection_selected"
            : "transform_selection_unselected";
        var textTips = PredictionHoverTips.Text(
            tipKey,
            description => description.Add("TransformedCard", replacement.Title));

        return textTips
            .Concat(PredictionHoverTips.Cards([replacement]))
            .ToList();
    }

    private CardModel? PredictReplacement(CardModel hoveredCard)
    {
        var previewCards = selectedCards.ToList();
        var hoveredSelectionIndex = previewCards.IndexOf(hoveredCard);
        var predictionSequence = hoveredSelectionIndex >= 0
            ? previewCards.Take(hoveredSelectionIndex + 1)
            : previewCards.Append(hoveredCard);

        (cardToTransformation.Target as TransformPreviewPredictor)?.Reset();

        CardTransformation? hoveredTransformation = null;
        foreach (var card in predictionSequence)
        {
            var transformation = cardToTransformation(card);
            if (card == hoveredCard)
            {
                hoveredTransformation = transformation;
            }
        }

        return hoveredTransformation?.Replacement;
    }
}

internal static class TransformSelectionHoverTips
{
    public static IReadOnlyList<IHoverTip> GetHoverTips(Control owner)
    {
        if (owner is not NGridCardHolder holder ||
            FindTransformSelectScreen(holder) is not { } screen)
        {
            return [];
        }

        var prediction = new TransformSelectionPrediction(
            screen._selectedCards,
            screen._cardToTransformation);

        return prediction.GetHoverTips(holder.CardModel);
    }

    private static NDeckTransformSelectScreen? FindTransformSelectScreen(NCardHolder holder)
    {
        for (var node = holder.GetParent(); node != null; node = node.GetParent())
        {
            if (node is NDeckTransformSelectScreen screen)
            {
                return screen;
            }
        }

        return null;
    }
}

[HarmonyPatch(typeof(NDeckTransformSelectScreen), "OnCardClicked")]
internal static class TransformSelectionHoverTipRefreshPatch
{
    private static void Postfix(NDeckTransformSelectScreen __instance, CardModel card)
    {
        var holder = __instance._grid.GetCardHolder(card);
        if (holder is not { _isHovered: true })
        {
            return;
        }

        NHoverTipSet.Remove(holder);
        if (!__instance._previewContainer.Visible)
        {
            holder.Call(NCardHolder.MethodName.CreateHoverTips);
        }
    }
}
