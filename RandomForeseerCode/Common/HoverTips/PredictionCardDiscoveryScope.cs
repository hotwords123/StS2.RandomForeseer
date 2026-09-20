using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using RandomForeseer.RandomForeseerCode.Utils;

namespace RandomForeseer.RandomForeseerCode.Common.HoverTips;

/// <summary>
/// Identifies prediction cards while their HoverTip nodes are being created, so vanilla card discovery is skipped.
/// </summary>
internal static class PredictionCardDiscoveryScope
{
    private static readonly HashSet<CardsSet> ActiveCardSets = [];

    public static IDisposable? Enter(CardHoverTip tip)
    {
        var cards = tip switch
        {
            PredictionCardHoverTip => new CardsSet([tip.Card]),
            PredictionCardBundleHoverTip bundle => new CardsSet([.. bundle.Cards]),
            _ => null
        };
        if (cards is null)
        {
            return null;
        }

        ActiveCardSets.Add(cards);
        return new DisposableAction(() => ActiveCardSets.Remove(cards));
    }

    public static bool Contains(CardModel card) => ActiveCardSets.Any(set => set.Cards.Contains(card));

    private sealed class CardsSet(CardModel[] cards)
    {
        public CardModel[] Cards { get; } = cards;
    }
}
