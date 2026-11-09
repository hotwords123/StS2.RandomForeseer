using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;

namespace RandomForeseer.Common;

internal static class PredictionUtils
{
    public static CardModel CreateCard(CardModel card, Player player)
    {
        card = (CardModel)card.MutableClone();
        card.Owner = player;
        return card;
    }

    /// <summary>
    /// Mirrors <see cref="CardCmd.Upgrade(CardModel, MegaCrit.Sts2.Core.Nodes.CommonUi.CardPreviewStyle)"/>.
    /// Does nothing if the card is not upgradable.
    /// </summary>
    public static void UpgradeCard(CardModel card)
    {
        if (!card.IsUpgradable)
        {
            return;
        }

        card.UpgradeInternal();
        card.FinalizeUpgradeInternal();
    }

    /// <summary>
    /// Same as <see cref="UpgradeCard"/>, but returns a new upgraded card instead of modifying the original card.
    /// Returns the original card if it is not upgradable.
    /// </summary>
    public static CardModel CreateUpgradedCard(CardModel card)
    {
        if (!card.IsUpgradable)
        {
            return card;
        }

        var previewCard = (CardModel)card.MutableClone();
        UpgradeCard(previewCard);
        return previewCard;
    }

    /// <summary>
    /// Mirrors <see cref="CardCmd.Enchant(EnchantmentModel, CardModel, decimal)"/>.
    /// Does nothing if the card cannot be enchanted by the given enchantment.
    /// </summary>
    public static void EnchantCard(EnchantmentModel enchantment, CardModel card, decimal amount)
    {
        if (!enchantment.CanEnchant(card))
        {
            return;
        }

        if (card.Enchantment is null)
        {
            card.EnchantInternal(enchantment, amount);
            enchantment.ModifyCard();
        }
        else
        {
            // The CanEnchant check above ensures that the existing enchantment is the same type as the new enchantment.
            card.Enchantment.Amount += (int)amount;
        }

        card.FinalizeUpgradeInternal();
    }

    /// <summary>
    /// Same as <see cref="EnchantCard"/>, but returns a new enchanted card instead of modifying the original card.
    /// Returns the original card if it cannot be enchanted by the given enchantment.
    /// </summary>
    /// <returns></returns>
    public static CardModel CreateEnchantedCard(EnchantmentModel enchantment, CardModel card, decimal amount)
    {
        if (!enchantment.CanEnchant(card))
        {
            return card;
        }

        var previewCard = (CardModel)card.MutableClone();
        EnchantCard(enchantment, previewCard, amount);
        return previewCard;
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
