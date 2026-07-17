using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Potions;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;

namespace RandomForeseer.RandomForeseerCode.Common;

internal static class PotionGenerationPrediction
{
    public static IReadOnlyList<IHoverTip> GetPotionHoverTips(PotionPredictionContext context)
    {
        if (!RandomForeseerSettings.IsPredictionFeatureEnabled(RandomForeseerSettings.EnablePotionGenerationPrediction))
        {
            return [];
        }

        return PredictionHoverTips.Potions(PredictPotions(context));
    }

    public static IReadOnlyList<IHoverTip> GetCardHoverTips(CardModel card)
    {
        if (!RandomForeseerSettings.IsPredictionFeatureEnabled(RandomForeseerSettings.EnablePotionGenerationPrediction) ||
            card is not Alchemize ||
            !CombatPredictionSimulator.TryCreate(card.Owner, out var simulator))
        {
            return [];
        }

        var predictedCard = simulator.State.FindCard(card) ?? new PredictedCard(card);
        simulator.ManualPlay(predictedCard, target: null);

        var history = simulator.History
            .OfType<CombatPredictionPotionGeneratedEntry>()
            .Where(entry => ReferenceEquals(entry.Trace?.Source, card))
            .ToList();
        var tips = PredictionHoverTips.Potions(history.Select(entry => entry.Potion)).ToList();
        var risk = simulator.History.GetRisk(history);
        PredictionHoverTips.AddDriftWarningIfNeeded(tips, "potion_generation", risk);
        return tips;
    }

    private static IReadOnlyList<PotionModel> PredictPotions(PotionPredictionContext context)
    {
        var source = context.Source;
        var target = context.Target;

        return source switch
        {
            EntropicBrew => PredictionUtils.PredictPotionRewards(
                target,
                // The player may discard existing potions before drinking Entropic Brew, so show
                // enough future results to fill the entire potion belt rather than only open slots.
                target.PotionSlots.Count,
                target.RunState.Rng.CombatPotionGeneration.Clone()),
            _ => []
        };
    }
}
