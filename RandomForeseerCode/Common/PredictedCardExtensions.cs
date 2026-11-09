using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace RandomForeseer.Common;

internal static class PredictedCardExtensions
{
    /// <summary>
    /// Mirrors <see cref="CardCmd.Upgrade(CardModel, CardPreviewStyle)"/>.
    /// </summary>
    public static PredictedCard Upgrade(this PredictedCard card)
    {
        if (card.Preview.IsUpgradable)
        {
            PredictionUtils.UpgradeCard(card.MutablePreview);
        }

        return card;
    }

    /// <summary>
    /// Mirrors <see cref="CardCmd.Enchant(EnchantmentModel, CardModel, decimal)"/>.
    /// </summary>
    public static PredictedCard Enchant(this PredictedCard card, EnchantmentModel enchantment, decimal amount)
    {
        if (enchantment.CanEnchant(card.Preview))
        {
            PredictionUtils.EnchantCard(enchantment, card.MutablePreview, amount);
        }

        return card;
    }

    // Upgrades the cards if the condition is true, otherwise returns the original cards.
    public static IEnumerable<PredictedCard> UpgradeIf(
        this IEnumerable<PredictedCard> cards,
        bool shouldUpgrade)
    {
        return shouldUpgrade ? cards.Select(card => card.Upgrade()) : cards;
    }

    /// <summary>
    /// Returns the original cards from the predicted cards.
    /// </summary>
    public static IEnumerable<CardModel> SelectOriginals(this IEnumerable<PredictedCard> cards)
    {
        return cards.Select(static card => card.Original);
    }

    /// <summary>
    /// Returns the preview cards from the predicted cards.
    /// </summary>
    public static IEnumerable<CardModel> SelectPreviews(this IEnumerable<PredictedCard> cards)
    {
        return cards.Select(static card => card.Preview);
    }
}
