using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Potions;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;

namespace RandomForeseer.RandomForeseerCode.Common;

internal static class PotionGenerationPrediction
{
    public static IReadOnlyList<IHoverTip> GetPotionHoverTips(PotionModel potion)
    {
        if (!RandomForeseerSettings.IsPredictionFeatureEnabled(RandomForeseerSettings.EnablePotionGenerationPrediction))
        {
            return [];
        }

        return PredictionHoverTips.Potions(PredictPotions(potion));
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

    private static IReadOnlyList<PotionModel> PredictPotions(PotionModel potion)
    {
        var owner = potion.Owner;

        return potion switch
        {
            EntropicBrew => PredictionUtils.PredictPotionRewards(
                owner,
                owner.PotionSlots.Count,
                owner.RunState.Rng.CombatPotionGeneration.Clone()),
            _ => []
        };
    }
}
