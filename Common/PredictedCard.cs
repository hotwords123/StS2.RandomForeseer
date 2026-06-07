using MegaCrit.Sts2.Core.Models;

namespace RandomForeseer.Common;

internal sealed class PredictedCard(CardModel original) : IComparable<PredictedCard>
{
    private CardModel? _preview;

    public CardModel Original { get; } = original;

    public CardModel Preview => _preview ?? Original;

    public CardModel MutablePreview => _preview ??= (CardModel)Original.MutableClone();

    public static List<PredictedCard> FromCards(IEnumerable<CardModel> cards)
    {
        return cards.Select(card => new PredictedCard(card)).ToList();
    }

    public int CompareTo(PredictedCard? other)
    {
        return Original.CompareTo(other?.Original);
    }
}
