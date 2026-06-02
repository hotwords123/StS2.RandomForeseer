using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using RandomForeseer.Common;

namespace RandomForeseer.OutOfCombat.Events;

internal static class ReflectionsPrediction
{
    public static void Register()
    {
        EventOptionPredictionRegistry.Register<Reflections>(GetHoverTips);
    }

    private static IReadOnlyList<IHoverTip> GetHoverTips(Reflections reflections, EventOption option)
    {
        if (option.TextKey != "REFLECTIONS.pages.INITIAL.options.TOUCH_A_MIRROR")
        {
            return [];
        }

        var player = reflections.Owner!;
        return PredictionHoverTips.Cards(PredictTouchAMirror(player.Deck.Cards, reflections.Rng));
    }

    private static IReadOnlyList<CardModel> PredictTouchAMirror(IReadOnlyList<CardModel> deckCards, MegaCrit.Sts2.Core.Random.Rng realRng)
    {
        var rng = PredictionUtils.CloneRng(realRng);
        var deckState = deckCards
            .Select(card => (CardModel)card.MutableClone())
            .ToList();
        var previews = new List<CardModel>();

        var upgradedCards = deckState
            .Where(card => card.IsUpgraded)
            .ToList();
        for (var i = 0; i < 2 && upgradedCards.Count > 0; i++)
        {
            var card = rng.NextItem(upgradedCards);
            if (card == null)
            {
                break;
            }

            upgradedCards.Remove(card);
            card.DowngradeInternal();
            previews.Add((CardModel)card.MutableClone());
        }

        var upgradableCards = deckState
            .Where(card => card.IsUpgradable)
            .ToList();
        for (var i = 0; i < 4 && upgradableCards.Count > 0; i++)
        {
            var card = rng.NextItem(upgradableCards);
            if (card == null)
            {
                break;
            }

            upgradableCards.Remove(card);
            previews.Add(PredictionUtils.ToUpgradedPreviewCard(card));
        }

        return previews;
    }
}
