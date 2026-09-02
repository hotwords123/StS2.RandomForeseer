using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models.Cards;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Cards.OnPlay;

internal static class ExhaustCardMirrors
{
    public static void SecondWindOnPlay(SecondWind card, CardOnPlayMirrorContext context)
    {
        var cardsToExhaust = context.OwnerState.Hand.Cards
            .Where(predicted => predicted.Preview.Type != CardType.Attack)
            .ToArray();

        foreach (var cardToExhaust in cardsToExhaust)
        {
            context.Simulator.Exhaust(cardToExhaust);
            context.GainBlock(card.Owner.Creature);
        }
    }
}
