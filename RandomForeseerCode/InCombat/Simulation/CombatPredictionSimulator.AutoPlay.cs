using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using RandomForeseer.RandomForeseerCode.Common;

namespace RandomForeseer.RandomForeseerCode.InCombat.Simulation;

internal sealed partial class CombatPredictionSimulator
{
    // Mirrors CardCmd.AutoPlay only as a risk marker for now. Real autoplay can move cards,
    // capture resources, run ShouldPlay/BeforeCardAutoPlayed, execute arbitrary OnPlay logic,
    // and change result piles; none of that is simulated yet.
    public void AutoPlay(PredictedCard card, Creature? target = null, AutoPlayType type = AutoPlayType.Default)
    {
        _ = target;
        _ = type;
        using (PushSource(card.Original))
        {
            MarkCurrentSourceRisky();
        }
    }

    // Mirrors CardPileCmd.AutoPlayFromDrawPile through selecting cards and moving them to
    // the play pile. Actual card autoplay is still a risk marker in AutoPlay.
    public IReadOnlyList<PredictedCard> AutoPlayFromDrawPile(
        Player player,
        int count,
        CardPilePosition position,
        bool forceExhaust)
    {
        var cards = MoveCardsForAutoPlay(player, count, position);

        foreach (var card in cards)
        {
            if (State.GetCreature(card.Original.Owner.Creature).IsDead)
            {
                break;
            }

            card.MutablePreview.ExhaustOnNextPlay = forceExhaust;
            AutoPlay(card);
        }

        return cards;
    }

    // Mirrors CardPileCmd.AutoPlayFromDrawPile until the card is moved to the play pile. Should be an internal
    // helper for AutoPlayFromDrawPile, but is public for draw-pile prediction without autoplay.
    public IReadOnlyList<PredictedCard> MoveCardsForAutoPlay(Player player, int count, CardPilePosition position)
    {
        var cards = new List<PredictedCard>(count);
        var playerCombatState = State.GetPlayerCombatState(player);
        var drawPile = playerCombatState.DrawPile;

        for (int i = 0; i < count; i++)
        {
            ShuffleIfNecessary(player);
            var card = position switch
            {
                CardPilePosition.Top => drawPile.TopCard,
                CardPilePosition.Bottom => drawPile.BottomCard,
                CardPilePosition.Random => Rng.CombatCardSelection.NextItem(drawPile.Cards),
                _ => null
            };

            if (card == null)
            {
                break;
            }

            cards.Add(card);
            AddToPile(card, playerCombatState.PlayPile);
        }

        return cards;
    }
}
