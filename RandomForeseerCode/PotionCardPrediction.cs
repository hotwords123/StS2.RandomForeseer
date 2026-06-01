using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Random;

namespace RandomForeseer;

internal static class PotionCardPrediction
{
    public static IReadOnlyList<IHoverTip> GetHoverTips(PotionModel potion)
    {
        if (!RandomForeseerSettings.EnablePotionCardPrediction)
        {
            return [];
        }

        var owner = potion.Owner;
        if (owner?.Creature.CombatState == null)
        {
            return [];
        }

        var previewRng = PredictionUtils.CloneRng(owner.RunState.Rng.CombatCardGeneration);
        var cards = PredictCards(potion, previewRng);
        return cards.Select(card => (IHoverTip)new PotionPredictionCardHoverTip(card)).ToList();
    }

    private static IReadOnlyList<CardModel> PredictCards(PotionModel potion, Rng previewRng)
    {
        return potion switch
        {
            AttackPotion => PredictCharacterCards(potion, CardType.Attack, 3, previewRng),
            SkillPotion => PredictCharacterCards(potion, CardType.Skill, 3, previewRng),
            PowerPotion => PredictCharacterCards(potion, CardType.Power, 3, previewRng),
            ColorlessPotion => PredictColorlessCards(potion, 3, previewRng),
            CosmicConcoction => PredictColorlessCards(potion, potion.DynamicVars.Cards.IntValue, previewRng)
                .Select(PredictionUtils.ToUpgradedPreviewCard)
                .ToList(),
            OrobicAcid => PredictOrobicAcid(potion, previewRng),
            _ => []
        };
    }

    private static IReadOnlyList<CardModel> PredictOrobicAcid(PotionModel potion, Rng previewRng)
    {
        var cards = new List<CardModel>();
        cards.AddRange(PredictCharacterCards(potion, CardType.Attack, 1, previewRng));
        cards.AddRange(PredictCharacterCards(potion, CardType.Skill, 1, previewRng));
        cards.AddRange(PredictCharacterCards(potion, CardType.Power, 1, previewRng));
        return cards;
    }

    private static IReadOnlyList<CardModel> PredictCharacterCards(
        PotionModel potion,
        CardType type,
        int count,
        Rng previewRng)
    {
        return PredictionUtils.TakeRandomDistinctCharacterCardsForCombat(potion.Owner, type, count, previewRng);
    }

    private static IReadOnlyList<CardModel> PredictColorlessCards(PotionModel potion, int count, Rng previewRng)
    {
        return PredictionUtils.TakeRandomDistinctColorlessCardsForCombat(potion.Owner, count, previewRng);
    }
}

internal sealed class PotionPredictionCardHoverTip(CardModel card) : PredictionCardHoverTip(card);
