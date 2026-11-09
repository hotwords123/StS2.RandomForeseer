using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace RandomForeseer.Common;

internal sealed class SimCardPile(PileType type, IEnumerable<PredictedCard> cards)
{
    private readonly List<PredictedCard> _cards = [.. cards];

    public PileType Type { get; } = type;

    public IReadOnlyList<PredictedCard> Cards => _cards;

    public bool IsEmpty => _cards.Count == 0;

    public PredictedCard? TopCard => IsEmpty ? null : _cards[0];

    public PredictedCard? BottomCard => IsEmpty ? null : _cards[^1];

    public SimCardPile(CardPile pile)
        : this(pile.Type, pile.Cards.Select(card => new PredictedCard(card)))
    {
    }

    public void Add(PredictedCard card)
    {
        _cards.Add(card);
    }

    public void Insert(int index, PredictedCard card)
    {
        _cards.Insert(index, card);
    }

    public bool Remove(PredictedCard card)
    {
        return _cards.Remove(card);
    }

    public void Clear()
    {
        _cards.Clear();
    }

    public SimCardPile Clone()
    {
        return new SimCardPile(Type, _cards.Select(card => card.Clone()));
    }

    public PredictedCard? Find(CardModel card)
    {
        return _cards.Find(predicted => predicted.References(card));
    }
}
