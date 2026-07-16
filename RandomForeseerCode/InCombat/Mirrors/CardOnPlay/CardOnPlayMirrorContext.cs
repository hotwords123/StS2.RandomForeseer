using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.CardOnPlay;

internal sealed class CardOnPlayMirrorContext : CombatPredictionCardMirrorContext<CardModel>
{
    public required CardPlay CardPlay { get; init; }

    public SimPlayerCombatState OwnerState => State.GetPlayerCombatState(PreviewCard.Owner);

    // The dispatch trace belongs to the card that caused the prediction, not its detached mutable preview.
    protected override AbstractModel GetDispatchSource(CardModel _) => OriginalCard;
}
