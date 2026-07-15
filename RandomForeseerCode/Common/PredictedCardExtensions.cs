namespace RandomForeseer.RandomForeseerCode.Common;

internal static class PredictedCardExtensions
{
    // Mirrors CardCmd.Upgrade.
    public static PredictedCard Upgrade(this PredictedCard card)
    {
        if (card.Preview.IsUpgradable)
        {
            var previewCard = card.MutablePreview;
            previewCard.UpgradeInternal();
            previewCard.FinalizeUpgradeInternal();
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
}
