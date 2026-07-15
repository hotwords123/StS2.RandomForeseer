using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;

namespace RandomForeseer.RandomForeseerCode.Common;

internal static class PredictionUtils
{
    public static Rng CloneRng(Rng rng)
    {
        var clone = new Rng(rng.Seed)
        {
            Counter = rng.Counter
        };

        // STS2 0.107.1 stores Rng state in MegaRandom's Xoshiro** state.
        // Copying it directly avoids replaying an ever-growing counter during predictions.
        clone._random._s0 = rng._random._s0;
        clone._random._s1 = rng._random._s1;
        clone._random._s2 = rng._random._s2;
        clone._random._s3 = rng._random._s3;
        return clone;
    }

    public static CardModel CreateCard(CardModel card, Player player)
    {
        card = (CardModel)card.MutableClone();
        card.Owner = player;
        return card;
    }

    public static void UpgradeCardInPlace(CardModel previewCard)
    {
        if (previewCard.IsUpgradable)
        {
            previewCard.UpgradeInternal();
            previewCard.FinalizeUpgradeInternal();
        }
    }

    public static CardModel ToUpgradedCard(CardModel card)
    {
        var previewCard = (CardModel)card.MutableClone();
        UpgradeCardInPlace(previewCard);
        return previewCard;
    }

    public static CardModel ToUpgradedCardIf(CardModel card, bool shouldUpgrade)
    {
        return shouldUpgrade
            ? ToUpgradedCard(card)
            : card;
    }

    public static IReadOnlyList<CardModel> ToUpgradedCardsIf(
        IReadOnlyList<CardModel> cards,
        bool shouldUpgrade)
    {
        return shouldUpgrade
            ? cards.Select(ToUpgradedCard).ToList()
            : cards;
    }

    public static RelicModel CreateRelic(RelicModel relic, Player player)
    {
        relic = (RelicModel)relic.MutableClone();
        relic.Owner = player;
        return relic;
    }

    public static PotionModel CreatePotion(PotionModel potion, Player player)
    {
        potion = (PotionModel)potion.MutableClone();
        potion.Owner = player;
        return potion;
    }

    public static CardModel PredictTransformResult(CardModel original, Rng rng, bool isInCombat)
    {
        var options = CardFactory.GetDefaultTransformationOptions(original, isInCombat);
        var result = rng.NextItem(options)
            ?? throw new InvalidOperationException($"Could not predict a transform result for {original.Id}.");
        return result;
    }

    public static IReadOnlyList<PotionModel> PredictPotionRewards(Player player, int count, Rng rng)
    {
        return Enumerable.Range(0, count)
            .Select(_ => PotionFactory.CreateRandomPotionOutOfCombat(player, rng))
            .ToList();
    }
}
