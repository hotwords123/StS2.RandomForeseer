using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace RandomForeseer.RandomForeseerCode.Common;

internal sealed class PredictedCard(
    CardModel original,
    CardModel? preview = null) : IComparable<PredictedCard>
{
    public CardModel Original => original;

    public CardModel Preview => preview ?? original;

    public CardModel MutablePreview => preview ??= (CardModel)original.MutableClone();

    public static List<PredictedCard> FromCards(IEnumerable<CardModel> cards)
    {
        return cards.Select(card => new PredictedCard(card)).ToList();
    }

    public static PredictedCard FromGenerated(CardModel card)
    {
        return new(card, card);
    }

    public static PredictedCard Create(CardModel canonicalCard, Player player)
    {
        return FromGenerated(PredictionUtils.CreateCard(canonicalCard, player));
    }

    public bool References(CardModel card)
    {
        return ReferenceEquals(original, card) || ReferenceEquals(preview, card);
    }

    public PredictedCard Upgrade()
    {
        PredictionUtils.UpgradeCardInPlace(MutablePreview);
        return this;
    }

    public PredictedCard Clone()
    {
        return new(original, (CardModel?)preview?.MutableClone());
    }

    public int CompareTo(PredictedCard? other)
    {
        return original.CompareTo(other?.Original);
    }
}
