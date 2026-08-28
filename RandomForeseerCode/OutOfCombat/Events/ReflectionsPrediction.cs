using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Random;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.Common.HoverTips;

namespace RandomForeseer.RandomForeseerCode.OutOfCombat.Events;

internal static class ReflectionsPrediction
{
    public static IReadOnlyList<IHoverTip> GetHoverTips(Reflections reflections, EventOption option)
    {
        if (option.TextKey != "REFLECTIONS.pages.INITIAL.options.TOUCH_A_MIRROR")
        {
            return [];
        }

        var player = reflections.Owner!;
        return [.. PredictTouchAMirror(player.Deck.Cards, reflections.Rng).ToPredictionHoverTips()];
    }

    private static IReadOnlyList<CardModel> PredictTouchAMirror(IReadOnlyList<CardModel> deckCards, Rng realRng)
    {
        var rng = realRng.Clone();
        var deckState = deckCards.Select(card => new PredictedCard(card)).ToList();
        var previews = new List<CardModel>();

        var upgradedCards = deckState
            .Where(card => card.Preview.IsUpgraded)
            .ToList();
        for (var i = 0; i < 2 && upgradedCards.Count > 0; i++)
        {
            var card = rng.NextItem(upgradedCards);
            if (card == null)
            {
                break;
            }

            upgradedCards.Remove(card);
            card.MutablePreview.DowngradeInternal();
            previews.Add(card.Preview);
        }

        var upgradableCards = deckState
            .Where(card => card.Preview.IsUpgradable)
            .ToList();
        for (var i = 0; i < 4 && upgradableCards.Count > 0; i++)
        {
            var card = rng.NextItem(upgradableCards);
            if (card == null)
            {
                break;
            }

            upgradableCards.Remove(card);
            previews.Add(PredictionUtils.CreateUpgradedCard(card.Preview));
        }

        return previews;
    }
}
