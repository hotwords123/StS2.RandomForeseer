using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using RandomForeseer.Common;

namespace RandomForeseer.OutOfCombat.Mirrors.Hooks.CardCreation;

internal static class CardCreationResultUtils
{
    public static void UpgradeCardsOfType(
        IEnumerable<CardCreationResult> results,
        RelicModel modifyingRelic,
        CardType cardType)
    {
        foreach (var result in results.Where(result => result.Card.Type == cardType && result.Card.IsUpgradable))
        {
            result.ModifyCard(PredictionUtils.CreateUpgradedCard(result.Card), modifyingRelic);
        }
    }

    public static void UpgradeValidCards(
        IEnumerable<CardCreationResult> results,
        RelicModel modifyingRelic)
    {
        foreach (var result in results.Where(result => result.Card.IsUpgradable))
        {
            result.ModifyCard(PredictionUtils.CreateUpgradedCard(result.Card), modifyingRelic);
        }
    }

    public static void EnchantValidCards<T>(
        IEnumerable<CardCreationResult> results,
        RelicModel modifyingRelic,
        decimal amount)
        where T : EnchantmentModel
    {
        var enchantment = ModelDb.Enchantment<T>();

        foreach (var result in results.Where(result => enchantment.CanEnchant(result.Card)))
        {
            var enchantedCard = PredictionUtils.CreateEnchantedCard(enchantment.ToMutable(), result.Card, amount);
            result.ModifyCard(enchantedCard, modifyingRelic);
        }
    }
}
