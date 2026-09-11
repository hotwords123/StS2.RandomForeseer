using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace RandomForeseer.RandomForeseerCode.Common;

internal sealed class PredictedCard(
    CardModel original,
    CardModel? preview = null) : IComparable<PredictedCard>
{
    private CardModel? _preview = preview;

    public CardModel Original { get; } = original;

    public CardModel Preview => _preview ?? Original;

    public CardModel MutablePreview => _preview ??= (CardModel)Original.MutableClone();

    public static PredictedCard FromGenerated(CardModel card)
    {
        return new PredictedCard(card, card);
    }

    public static PredictedCard Create(CardModel canonicalCard, Player player)
    {
        return FromGenerated(PredictionUtils.CreateCard(canonicalCard, player));
    }

    public bool References(object? card)
    {
        return ReferenceEquals(Original, card) || ReferenceEquals(Preview, card);
    }

    // Clones the prediction wrapper state only. Combat effects that generate a gameplay
    // clone of a card should use CombatPredictedCardExtensions.CreateClone instead.
    public PredictedCard Clone()
    {
        return new PredictedCard(Original, (CardModel?)_preview?.MutableClone());
    }

    public int CompareTo(PredictedCard? other)
    {
        return Preview.CompareTo(other?.Preview);
    }
}
