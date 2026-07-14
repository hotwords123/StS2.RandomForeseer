using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.CardOnPlay;

internal sealed class CardOnPlayMirrorContext : CombatPredictionCardMirrorContext<CardModel>
{
    public required CardPlay CardPlay { get; init; }

    public SimPlayerCombatState OwnerState => State.GetPlayerCombatState(PreviewCard.Owner);

    // Registry risk belongs to the card that caused the prediction, not its detached mutable preview.
    public override IDisposable PushSource(CardModel _)
    {
        return Simulator.PushSource(OriginalCard);
    }
}
