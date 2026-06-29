using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;

namespace RandomForeseer.Common;

internal static class TransformPrediction
{
    public static IReadOnlyList<IHoverTip> GetHoverTips(
        CardModel card,
        IEnumerable<CardModel> selectedCards,
        int maxSelect,
        Rng rng,
        bool isInCombat,
        Func<CardModel, CardModel>? mapReplacement = null)
    {
        var replacements = PredictReplacements(card, rng, maxSelect, isInCombat, mapReplacement);
        if (replacements.Count == 0)
        {
            return [];
        }

        var selectedList = selectedCards.ToList();
        var activeIndex = selectedList.IndexOf(card);
        var isSelected = activeIndex >= 0;
        if (!isSelected)
        {
            activeIndex = Math.Min(maxSelect - 1, selectedList.Count);
        }

        var tipKey = isSelected
            ? "transform_selection_selected"
            : "transform_selection_unselected";

        var transformedCard = replacements[activeIndex].Title;
        var otherTransformedCards = replacements
            .Where((_, index) => index != activeIndex)
            .Select(card => card.Title)
            .Distinct()
            .ToList();

        var textTip = PredictionHoverTips.Text(
            tipKey,
            description =>
            {
                description.Add("TransformedCard", transformedCard);
                description.Add("HasOtherTransformedCards", otherTransformedCards.Count > 0);
                description.Add("OtherTransformedCards", otherTransformedCards);
            });

        var cardTips = replacements
            .Select((replacement, index) => (IHoverTip)new PredictionCardHoverTip(
                replacement,
                isDimmed: index != activeIndex))
            .ToList();

        return cardTips.Prepend(textTip).ToList();
    }

    private static IReadOnlyList<CardModel> PredictReplacements(
        CardModel card,
        Rng rng,
        int maxSelect,
        bool isInCombat,
        Func<CardModel, CardModel>? mapReplacement)
    {
        if (maxSelect <= 0)
        {
            return [];
        }

        var previewRng = PredictionUtils.CloneRng(rng);
        return Enumerable.Range(0, maxSelect)
            .Select(_ => PredictionUtils.PredictTransformResult(card, previewRng, isInCombat))
            .Select(replacement => mapReplacement?.Invoke(replacement) ?? replacement)
            .ToList();
    }
}
