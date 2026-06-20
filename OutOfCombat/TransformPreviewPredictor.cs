using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using RandomForeseer.Common;

namespace RandomForeseer.OutOfCombat;

internal sealed class TransformPreviewPredictor(Rng realRng, bool upgradePreview)
{
    private Rng _previewRng = PredictionUtils.CloneRng(realRng);

    public static Func<CardModel, CardTransformation>? Make(
        Rng realRng,
        bool upgradePreview = false,
        PredictionFairness fairness = PredictionFairness.Fair)
    {
        if (!RandomForeseerSettings.IsPredictionFeatureEnabled(RandomForeseerSettings.EnableTransformPrediction) ||
            !RandomForeseerSettings.IsFairPredictionAllowed(fairness))
        {
            return null;
        }

        return new TransformPreviewPredictor(realRng, upgradePreview).Predict;
    }

    public void Reset()
    {
        _previewRng = PredictionUtils.CloneRng(realRng);
    }

    public IReadOnlyList<IHoverTip> GetHoverTips(CardModel card, IEnumerable<CardModel> selectedCards, int maxSelect)
    {
        return TransformPrediction.GetHoverTips(
            card,
            selectedCards,
            maxSelect,
            _previewRng,
            isInCombat: false,
            mapReplacement: replacement => PredictionUtils.ToUpgradedCardIf(replacement, upgradePreview));
    }

    public CardTransformation Predict(CardModel original)
    {
        var predicted = PredictionUtils.PredictTransformResult(original, _previewRng, isInCombat: false);

        return new CardTransformation(
            original,
            PredictionUtils.ToUpgradedCardIf(predicted, upgradePreview));
    }
}
