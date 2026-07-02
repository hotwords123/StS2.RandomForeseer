using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace RandomForeseer.RandomForeseerCode.Common;

internal sealed class SimCardPile(PileType type, List<PredictedCard> cards)
{
    public PileType Type => type;

    public IReadOnlyList<PredictedCard> Cards => cards;

    public bool IsEmpty => cards.Count == 0;

    public PredictedCard? TopCard => IsEmpty ? null : cards[0];

    public PredictedCard? BottomCard => IsEmpty ? null : cards[^1];

    public static SimCardPile FromPlayerPile(PileType type, Player player)
    {
        return new(type, PredictedCard.FromCards(type.GetPile(player).Cards));
    }

    public void Add(PredictedCard card)
    {
        cards.Add(card);
    }

    public void Insert(int index, PredictedCard card)
    {
        cards.Insert(index, card);
    }

    public void AddRange(IEnumerable<PredictedCard> cardsToAdd)
    {
        cards.AddRange(cardsToAdd);
    }

    public bool Remove(PredictedCard card)
    {
        return cards.Remove(card);
    }

    public void Clear()
    {
        cards.Clear();
    }

    public SimCardPile Clone()
    {
        return new(type, cards.Select(card => card.Clone()).ToList());
    }

    public PredictedCard? Find(CardModel card)
    {
        return cards.FirstOrDefault(predicted => predicted.References(card));
    }
}
