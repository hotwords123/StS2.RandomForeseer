using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;

namespace RandomForeseer;

internal static class SlipperyBridgePrediction
{
    public static void Register()
    {
        EventOptionPredictionRegistry.Register<SlipperyBridge>(GetHoverTips);
    }

    private static IReadOnlyList<IHoverTip> GetHoverTips(SlipperyBridge slipperyBridge, EventOption option)
    {
        if (!option.TextKey.Contains("HOLD_ON", StringComparison.Ordinal))
        {
            return [];
        }

        var player = slipperyBridge.Owner!;
        var rng = PredictionUtils.CloneRng(slipperyBridge.Rng);
        var current = slipperyBridge._randomCardToLose;
        var skipped = slipperyBridge._skippedRemovals?.ToHashSet() ?? [];
        var cards = new List<CardModel>();

        for (var i = 0; i < RandomForeseerSettings.SlipperyBridgeRerollPreviewCount; i++)
        {
            if (current != null)
            {
                skipped.Add(current);
            }

            var candidates = current == null
                ? player.Deck.Cards.Where(card => card.Rarity != CardRarity.Basic).ToList()
                : player.Deck.Cards.Where(card => card.GetType() != current.GetType()).ToList();
            candidates.RemoveAll(card => !card.IsRemovable || skipped.Contains(card));
            if (candidates.Count == 0)
            {
                candidates = player.Deck.Cards.Where(card => card.IsRemovable).ToList();
            }

            current = rng.NextItem(candidates);
            if (current == null)
            {
                break;
            }

            cards.Add((CardModel)current.MutableClone());
        }

        return PredictionHoverTips.Cards(cards);
    }
}
