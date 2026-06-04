using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Potions;
using RandomForeseer.Common;

namespace RandomForeseer.InCombat;

internal static class PotionGenerationPrediction
{
    public static IReadOnlyList<IHoverTip> GetPotionHoverTips(Player player, PotionModel potion)
    {
        if (!RandomForeseerSettings.IsPredictionFeatureEnabled(RandomForeseerSettings.EnablePotionGenerationPrediction))
        {
            return [];
        }

        var potions = PredictPotions(player, potion);
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

        var potions = PredictPotions(card);
        return PredictionHoverTips.Potions(potions);
    }

    private static IReadOnlyList<PotionModel> PredictPotions(Player player, PotionModel potion)
    {
        return potion switch
        {
            EntropicBrew => PredictionUtils.PredictOutOfCombatPotionRewards(
                player,
                player.PotionSlots.Count,
                player.RunState.Rng.CombatPotionGeneration),
            _ => []
        };
    }

    private static IReadOnlyList<PotionModel> PredictPotions(CardModel card)
    {
        return card switch
        {
            Alchemize => [PredictionUtils.PredictInCombatPotion(card.Owner, card.Owner.RunState.Rng.CombatPotionGeneration)],
            _ => []
        };
    }
}
