using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using RandomForeseer.Common;

namespace RandomForeseer.OutOfCombat;

internal static class TransformPreviewPredictor
{
    public static Func<CardModel, CardTransformation>? Make(Rng realRng, bool upgradePreview = false)
    {
        if (!RandomForeseerSettings.EnableTransformPrediction)
        {
            return null;
        }

        return new TransformPreviewPredictionSession(realRng, upgradePreview).Predict;
    }

    private sealed class TransformPreviewPredictionSession : IResettableTransformPreviewPredictor
    {
        private readonly Rng _realRng;
        private readonly bool _upgradePreview;

        private Rng _previewRng;

        public TransformPreviewPredictionSession(Rng realRng, bool upgradePreview)
        {
            _realRng = realRng;
            _upgradePreview = upgradePreview;
            _previewRng = PredictionUtils.CloneRng(realRng);
        }

        public void Reset()
        {
            _previewRng = PredictionUtils.CloneRng(_realRng);
        }

        public CardTransformation Predict(CardModel original)
        {
            var options = CardFactory.GetDefaultTransformationOptions(
                original,
                original.CombatState != null);

            var predicted = _previewRng.NextItem(options);
            if (predicted == null)
            {
                return new CardTransformation(original);
            }

            return new CardTransformation(
                original,
                _upgradePreview
                    ? PredictionUtils.ToUpgradedPreviewCard(predicted)
                    : predicted);
        }
    }
}

internal interface IResettableTransformPreviewPredictor
{
    void Reset();
}
