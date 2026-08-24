using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models.Cards;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Cards.OnPlay;

internal static class AutoPlayCardMirrors
{
    public static void HavocOnPlay(Havoc card, CardOnPlayMirrorContext context)
    {
        context.Simulator.AutoPlayFromDrawPile(card.Owner, 1, CardPilePosition.Top, forceExhaust: true);
    }

    public static void CascadeOnPlay(Cascade card, CardOnPlayMirrorContext context)
    {
        var count = context.ResolveEnergyXValue() + (card.IsUpgraded ? 1 : 0);
        context.Simulator.AutoPlayFromDrawPile(card.Owner, count, CardPilePosition.Top);
    }
}
