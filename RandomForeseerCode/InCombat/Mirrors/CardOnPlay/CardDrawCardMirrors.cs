using MegaCrit.Sts2.Core.Models.Cards;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.CardOnPlay;

internal static class CardDrawCardMirrors
{
    public static void CalculatedGambleOnPlay(CalculatedGamble card, CardOnPlayMirrorContext context)
    {
        var cards = context.OwnerState.Hand.Cards.ToArray();
        context.Simulator.DiscardAndDraw(cards, cards.Length);
    }

    public static void RebootOnPlay(Reboot card, CardOnPlayMirrorContext context)
    {
        context.Simulator.MoveHandToDrawPile(card.Owner);
        context.Simulator.Shuffle(card.Owner);
        context.Simulator.Draw(card.Owner, card.DynamicVars.Cards.IntValue);
    }
}
