using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace RandomForeseer.RandomForeseerCode.InCombat.Simulation;

internal sealed partial class CombatPredictionSimulator
{
    // Mirrors CardCmd.AutoPlay only as a risk marker for now. Real autoplay can move cards,
    // capture resources, run ShouldPlay/BeforeCardAutoPlayed, execute arbitrary OnPlay logic,
    // and change result piles; none of that is simulated yet.
    public void AutoPlay(CardModel card, Creature? target = null, AutoPlayType type = AutoPlayType.Default)
    {
        _ = target;
        _ = type;
        using (PushSource(card))
        {
            MarkCurrentSourceRisky();
        }
    }

    // Mirrors CardPileCmd.AutoPlayFromDrawPile only as a risk marker for now. It records the
    // cards that are currently visible from the requested draw-pile edge. If the real command
    // would need a shuffle or random selection, record the triggering source as uncertain.
    public void AutoPlayFromDrawPile(
        Player player,
        int count,
        CardPilePosition position,
        bool forceExhaust,
        AbstractModel source)
    {
        _ = forceExhaust;

        if (count <= 0)
        {
            return;
        }

        var drawPileCards = State.GetPlayerCombatState(player).DrawPileCards;
        var cards = position switch
        {
            CardPilePosition.Top => drawPileCards.Take(count).ToList(),
            CardPilePosition.Bottom => Enumerable.Reverse(drawPileCards).Take(count).ToList(),
            CardPilePosition.Random => [],
            _ => []
        };

        if (cards.Count < count || position == CardPilePosition.Random)
        {
            using (PushSource(source))
            {
                MarkCurrentSourceRisky();
            }
        }

        foreach (var card in cards)
        {
            AutoPlay(card.Preview);
        }
    }
}
