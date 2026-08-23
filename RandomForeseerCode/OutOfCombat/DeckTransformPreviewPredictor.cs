using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using RandomForeseer.RandomForeseerCode.Common;

namespace RandomForeseer.RandomForeseerCode.OutOfCombat;

/// <summary>
/// Owns cloned RNG state for one deck-transform selection screen.
/// </summary>
internal sealed class DeckTransformPreviewPredictor(Rng realRng, bool upgradePreview = false)
{
    private Rng _previewRng = realRng.Clone();

    /// <summary>
    /// Realigns sequential preview generation when the same selection screen opens its preview again.
    /// </summary>
    public void Reset()
    {
        _previewRng = realRng.Clone();
    }

    public IReadOnlyList<IHoverTip> GetHoverTips(CardModel card, IEnumerable<CardModel> selectedCards, int maxSelect)
    {
        return TransformPrediction.GetHoverTips(
            card,
            selectedCards,
            maxSelect,
            realRng,
            isInCombat: false,
            upgradePreview ? PredictionUtils.CreateUpgradedCard : null);
    }

    /// <summary>
    /// Predicts the next replacement while advancing only this screen's cloned RNG.
    /// </summary>
    public CardTransformation PredictNext(CardModel original)
    {
        var predicted = PredictionUtils.PredictTransformResult(original, _previewRng, isInCombat: false);
        if (upgradePreview)
        {
            predicted = PredictionUtils.CreateUpgradedCard(predicted);
        }

        return new CardTransformation(original, predicted);
    }
}
