using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using RandomForeseer.Common;

namespace RandomForeseer.OutOfCombat;

internal static class TransformPreviewPredictor
{
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

        return new TransformPreviewPredictionSession(realRng, upgradePreview).Predict;
    }

    private sealed class TransformPreviewPredictionSession(Rng realRng, bool upgradePreview) : IResettableTransformPreviewPredictor
    {
        private Rng _previewRng = PredictionUtils.CloneRng(realRng);

        public void Reset()
        {
            _previewRng = PredictionUtils.CloneRng(realRng);
        }

        public CardTransformation Predict(CardModel original)
        {
            var predicted = PredictionUtils.PredictTransformResult(original, _previewRng, isInCombat: false);

            return new CardTransformation(
                original,
                PredictionUtils.ToUpgradedCardIf(predicted, upgradePreview));
        }
    }
}

internal interface IResettableTransformPreviewPredictor
{
    void Reset();
}
