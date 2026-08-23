using System.Diagnostics.CodeAnalysis;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Relics;
using RandomForeseer.RandomForeseerCode.Data;

namespace RandomForeseer.RandomForeseerCode.OutOfCombat;

/// <summary>
/// Resolves an active deck-transform source into its exact preview RNG and presentation rules.
/// </summary>
internal static class DeckTransformPrediction
{
    public static bool TryCreatePredictor(
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
                new DeckTransformPreviewPredictor(relic.Owner.RunState.Rng.Niche, upgradePreview: true),

            DeckTransformPredictionSource.Relic { Model: NewLeaf relic }
                when IsPredictionAllowedForRelic(settings, relic) =>
                new DeckTransformPreviewPredictor(relic.Owner.RunState.Rng.Niche),

            DeckTransformPredictionSource.Event
            {
                Model: AromaOfChaos eventModel,
                Option.TextKey: "AROMA_OF_CHAOS.pages.INITIAL.options.LET_GO"
            } => new DeckTransformPreviewPredictor(eventModel.Rng),

            DeckTransformPredictionSource.Event
            {
                Model: EndlessConveyor eventModel,
                Option.TextKey: "ENDLESS_CONVEYOR.pages.ALL.options.JELLY_LIVER"
            } => new DeckTransformPreviewPredictor(eventModel.Rng),

            DeckTransformPredictionSource.Event
            {
                Model: MorphicGrove eventModel,
                Option.TextKey: "MORPHIC_GROVE.pages.INITIAL.options.GROUP"
            } => new DeckTransformPreviewPredictor(eventModel.Rng),

            DeckTransformPredictionSource.Event
            {
                Model: Symbiote eventModel,
                Option.TextKey: "SYMBIOTE.pages.INITIAL.options.KILL_WITH_FIRE"
            } => new DeckTransformPreviewPredictor(eventModel.Rng),

            DeckTransformPredictionSource.Event
            {
                Model: Trial eventModel,
                Option.TextKey: "TRIAL.pages.NONDESCRIPT.options.INNOCENT"
            } => new DeckTransformPreviewPredictor(eventModel.Rng),

            DeckTransformPredictionSource.Event
            {
                Model: WhisperingHollow eventModel,
                Option.TextKey: "WHISPERING_HOLLOW.pages.INITIAL.options.HUG"
            } => new DeckTransformPreviewPredictor(eventModel.Rng),

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
