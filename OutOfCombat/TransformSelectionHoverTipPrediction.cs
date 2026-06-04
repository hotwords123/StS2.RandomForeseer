using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using RandomForeseer.Common;

namespace RandomForeseer.OutOfCombat;

internal static class TransformSelectionHoverTipPrediction
{
    public static IReadOnlyList<IHoverTip> GetHoverTips(
        NDeckTransformSelectScreen screen,
        NGridCardHolder holder)
    {
        var hoveredCard = holder.CardModel;
        if (hoveredCard == null)
        {
            return [];
        }

        var replacement = PredictReplacement(screen, hoveredCard);
        return replacement == null
            ? []
            : PredictionHoverTips.Cards([replacement]);
    }

    private static CardModel? PredictReplacement(NDeckTransformSelectScreen screen, CardModel hoveredCard)
    {
        var cardToTransformation = screen._cardToTransformation;
        var selectedCards = screen._selectedCards.ToList();
        var hoveredSelectionIndex = selectedCards.IndexOf(hoveredCard);
        var predictionSequence = hoveredSelectionIndex >= 0
            ? selectedCards.Take(hoveredSelectionIndex + 1)
            : selectedCards.Append(hoveredCard);

        (cardToTransformation.Target as IResettableTransformPreviewPredictor)?.Reset();

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

[HarmonyPatch(typeof(NCardHolder), "CreateHoverTips")]
internal static class TransformSelectionHoverTipPatch
{
    private static bool Prefix(NCardHolder __instance)
    {
        if (__instance is not NGridCardHolder holder ||
            holder.CardNode is not { } cardNode ||
            cardNode.Model is not { } cardModel ||
            FindTransformSelectScreen(holder) is not { } screen)
        {
            return true;
        }

        var predictionTips = TransformSelectionHoverTipPrediction.GetHoverTips(screen, holder);
        if (predictionTips.Count == 0)
        {
            return true;
        }

        var tips = cardModel.HoverTips.Concat(predictionTips).ToList();
        NHoverTipSet.CreateAndShow(holder, tips)?.SetAlignmentForCardHolder(holder);
        return false;
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
