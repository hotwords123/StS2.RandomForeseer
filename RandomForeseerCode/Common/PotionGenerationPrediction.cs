using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Potions;

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
        if (!RandomForeseerSettings.IsPredictionFeatureEnabled(RandomForeseerSettings.EnablePotionGenerationPrediction))
        {
            return [];
        }

        var potions = PredictPotions(card);
        return PredictionHoverTips.Potions(potions);
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

    private static IReadOnlyList<PotionModel> PredictPotions(CardModel card)
    {
        return card switch
        {
            Alchemize => [PotionFactory.CreateRandomPotionInCombat(
                card.Owner,
                card.Owner.RunState.Rng.CombatPotionGeneration.Clone())],
            _ => []
        };
    }
}
