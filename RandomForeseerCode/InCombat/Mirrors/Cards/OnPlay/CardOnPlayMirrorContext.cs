using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using RandomForeseer.InCombat.Simulation;

namespace RandomForeseer.InCombat.Mirrors.Cards.OnPlay;

internal sealed class CardOnPlayMirrorContext : CombatCardMirrorContext<CardModel>
{
    public required CardPlay CardPlay { get; init; }

    /// <summary>
    /// The target creature of the card play. Should only be used when the card play is known to have a target.
    /// Otherwise, use <see cref="CardPlay.Target"/> directly.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if the card play has no target.</exception>
    public Creature Target => CardPlay.Target
        ?? throw new InvalidOperationException("CardPlay has no target creature.");

    /// <summary>
    /// The target player of the card play. Should only be used when the card play is known to have a target,
    /// and the target is a player.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if the card play has no target player.</exception>
    public Player TargetPlayer => CardPlay.Target?.Player
        ?? throw new InvalidOperationException("CardPlay has no target player.");

    public SimPlayerCombatState OwnerState => State.GetPlayerCombatState(PreviewCard.Owner);

    // The dispatch trace belongs to the card that caused the prediction, not its detached mutable preview.
    protected override AbstractModel GetDispatchSource(CardModel _) => OriginalCard;
}
