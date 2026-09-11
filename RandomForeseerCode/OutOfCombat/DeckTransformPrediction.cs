using System.Diagnostics.CodeAnalysis;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Random;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.Data;

namespace RandomForeseer.RandomForeseerCode.OutOfCombat;

/// <summary>
/// Resolves an active deck-transform source into its exact preview RNG and presentation rules.
/// </summary>
internal static class DeckTransformPrediction
{
    public static bool TryCreatePredictor(
        CardSelectorPrefs prefs,
        DeckTransformPredictionSource source,
        [NotNullWhen(true)] out DeckTransformPreviewPredictor? predictor)
    {
        var settings = ModData.Settings;
        if (!settings.IsPredictionEnabled || !settings.DeckTransformPredictionEnabled)
        {
            predictor = null;
            return false;
        }

        predictor = source switch
        {
            DeckTransformPredictionSource.Relic { Model: Astrolabe relic }
                when IsPredictionAllowedForRelic(settings, relic) =>
                new DeckTransformPreviewPredictor(prefs.MaxSelect, relic.Owner.RunState.Rng.Niche, upgradePreview: true),

            DeckTransformPredictionSource.Relic { Model: NewLeaf relic }
                when IsPredictionAllowedForRelic(settings, relic) =>
                new DeckTransformPreviewPredictor(prefs.MaxSelect, relic.Owner.RunState.Rng.Niche),

            DeckTransformPredictionSource.Event
            {
                Model: AromaOfChaos eventModel,
                Option.TextKey: "AROMA_OF_CHAOS.pages.INITIAL.options.LET_GO"
            } => new DeckTransformPreviewPredictor(prefs.MaxSelect, eventModel.Rng),

            DeckTransformPredictionSource.Event
            {
                Model: EndlessConveyor eventModel,
                Option.TextKey: "ENDLESS_CONVEYOR.pages.ALL.options.JELLY_LIVER"
            } => new DeckTransformPreviewPredictor(prefs.MaxSelect, eventModel.Rng),

            DeckTransformPredictionSource.Event
            {
                Model: MorphicGrove eventModel,
                Option.TextKey: "MORPHIC_GROVE.pages.INITIAL.options.GROUP"
            } => new DeckTransformPreviewPredictor(prefs.MaxSelect, eventModel.Rng),

            DeckTransformPredictionSource.Event
            {
                Model: Symbiote eventModel,
                Option.TextKey: "SYMBIOTE.pages.INITIAL.options.KILL_WITH_FIRE"
            } => new DeckTransformPreviewPredictor(prefs.MaxSelect, eventModel.Rng),

            DeckTransformPredictionSource.Event
            {
                Model: Trial eventModel,
                Option.TextKey: "TRIAL.pages.NONDESCRIPT.options.INNOCENT"
            } => new DeckTransformPreviewPredictor(prefs.MaxSelect, eventModel.Rng),

            DeckTransformPredictionSource.Event
            {
                Model: WhisperingHollow eventModel,
                Option.TextKey: "WHISPERING_HOLLOW.pages.INITIAL.options.HUG"
            } => new DeckTransformPreviewPredictor(prefs.MaxSelect, eventModel.Rng),

            _ => null
        };

        return predictor is not null;
    }

    private static bool IsPredictionAllowedForRelic(ModSettings settings, RelicModel relic)
    {
        return settings.Allows(PredictionFairness.UnfairInSingleplayer) ||
               RewardPagePredictionContext.HasOtherPendingReward(relic);
    }
}

/// <summary>
/// Owns cloned RNG state for one deck-transform selection screen.
/// </summary>
internal sealed class DeckTransformPreviewPredictor(int maxSelect, Rng realRng, bool upgradePreview = false)
{
    private readonly TransformPrediction _prediction = new(
        maxSelect,
        isInCombat: false,
        mapReplacement: upgradePreview ? PredictionUtils.CreateUpgradedCard : null);

    private Rng _previewRng = realRng.Clone();

    /// <summary>
    /// Realigns sequential preview generation when the same selection screen opens its preview again.
    /// </summary>
    public void Reset()
    {
        _previewRng = realRng.Clone();
    }

    public IReadOnlyList<IHoverTip> GetHoverTips(CardModel card, IEnumerable<CardModel> selectedCards)
    {
        return _prediction.GetHoverTips(card, selectedCards, realRng.Clone());
    }

    /// <summary>
    /// Predicts the next replacement while advancing only this screen's cloned RNG.
    /// </summary>
    public CardTransformation PredictNext(CardModel original)
    {
        var predicted = _prediction.PredictNext(original, _previewRng);
        return new CardTransformation(original, predicted);
    }
}
