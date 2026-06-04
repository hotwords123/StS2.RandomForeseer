using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Random;
using RandomForeseer.Common;

namespace RandomForeseer.InCombat;

internal static class PotionGenerationPrediction
{
    public static IReadOnlyList<IHoverTip> GetPotionHoverTips(PotionModel potion)
    {
        if (!RandomForeseerSettings.IsPredictionFeatureEnabled(RandomForeseerSettings.EnablePotionGenerationPrediction))
        {
            return [];
        }

        var owner = potion.Owner;
        var previewRng = PredictionUtils.CloneRng(owner.RunState.Rng.CombatPotionGeneration);
        var potions = PredictPotions(potion, previewRng);
        return PredictionHoverTips.Potions(potions);
    }

    public static IReadOnlyList<IHoverTip> GetCardHoverTips(CardModel card)
    {
        if (!RandomForeseerSettings.IsPredictionFeatureEnabled(RandomForeseerSettings.EnablePotionGenerationPrediction) ||
            !card.IsMutable ||
            card.Pile?.Type != PileType.Hand ||
            card.Owner.Creature.CombatState == null)
        {
            return [];
        }

        var previewRng = PredictionUtils.CloneRng(card.Owner.RunState.Rng.CombatPotionGeneration);
        var potions = PredictPotions(card, previewRng);
        return PredictionHoverTips.Potions(potions);
    }

    private static IReadOnlyList<PotionModel> PredictPotions(PotionModel potion, Rng previewRng)
    {
        return potion switch
        {
            EntropicBrew => PredictEntropicBrew(potion, previewRng),
            _ => []
        };
    }

    private static IReadOnlyList<PotionModel> PredictPotions(CardModel card, Rng previewRng)
    {
        return card switch
        {
            Alchemize => [PotionFactory.CreateRandomPotionInCombat(card.Owner, previewRng).ToMutable()],
            _ => []
        };
    }

    private static IReadOnlyList<PotionModel> PredictEntropicBrew(PotionModel potion, Rng previewRng)
    {
        var owner = potion.Owner;
        return Enumerable.Range(0, owner.PotionSlots.Count)
            .Select(_ => PotionFactory.CreateRandomPotionOutOfCombat(owner, previewRng).ToMutable())
            .ToList();
    }
}
