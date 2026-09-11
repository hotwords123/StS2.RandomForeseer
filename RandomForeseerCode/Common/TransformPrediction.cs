using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using RandomForeseer.RandomForeseerCode.Common.HoverTips;

namespace RandomForeseer.RandomForeseerCode.Common;

internal sealed class TransformPrediction(
    int maxSelect,
    bool isInCombat,
    Func<CardModel, CardModel>? mapReplacement = null)
{
    public IReadOnlyList<IHoverTip> GetHoverTips(CardModel card, IEnumerable<CardModel> selectedCards, Rng previewRng)
    {
        var replacements = PredictReplacements(card, previewRng);
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
            .Select(replacement => replacement.Title)
            .Distinct()
            .ToList();

        var textTip = PredictionHoverTipFactory.Text(tipKey, description =>
        {
            description.Add("TransformedCard", transformedCard);
            description.Add("HasOtherTransformedCards", otherTransformedCards.Count > 0);
            description.Add("OtherTransformedCards", otherTransformedCards);
        });

        var cardTips = replacements.Select((replacement, index) =>
            PredictionHoverTipFactory.Card(replacement, isDimmed: index != activeIndex));

        return [textTip, .. cardTips];
    }

    public IReadOnlyList<CardModel> PredictReplacements(CardModel card, Rng previewRng)
    {
        if (maxSelect <= 0)
        {
            return [];
        }

        return [.. Enumerable.Range(0, maxSelect).Select(_ => PredictNext(card, previewRng))];
    }

    public CardModel PredictReplacement(CardModel card, int selectionIndex, Rng previewRng)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(selectionIndex);

        previewRng.Advance(selectionIndex);
        return PredictNext(card, previewRng);
    }

    public CardModel PredictNext(CardModel card, Rng previewRng)
    {
        var replacement = PredictionUtils.PredictTransformResult(card, previewRng, isInCombat);
        return mapReplacement is not null
            ? mapReplacement(replacement)
            : replacement;
    }
}
