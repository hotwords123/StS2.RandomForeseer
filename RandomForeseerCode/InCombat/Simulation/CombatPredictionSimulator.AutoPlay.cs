using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using RandomForeseer.RandomForeseerCode.Common;

namespace RandomForeseer.RandomForeseerCode.InCombat.Simulation;

internal sealed partial class CombatPredictionSimulator
{
    // Mirrors CardCmd.AutoPlay.
    public void AutoPlay(
        PredictedCard card,
        Creature? target = null,
        AutoPlayType type = AutoPlayType.Default,
        bool skipXCapture = false)
    {
        if (card.GetKeywords(State).Contains(CardKeyword.Unplayable) ||
            !Hook.ShouldPlay(State.CombatState, card.Preview, out var _, type) ||
            !TryResolveAutoPlayTarget(card, ref target))
        {
            MoveToResultPileWithoutPlaying(card);
            return;
        }

        if (card.GetPile(State) is null)
        {
            AddToPile(card, PileType.Play);
        }

        // TODO: Dispatch Hook.BeforeCardAutoPlayed
        var resources = SpendResources(card, isAutoPlay: true, skipXCapture);
        OnPlayWrapper(card, target, isAutoPlay: true, resources);
    }

    // Mirrors CardPileCmd.AutoPlayFromDrawPile through selecting cards and moving them to
    // the play pile, then runs each card through the shared AutoPlay path.
    public void AutoPlayFromDrawPile(
        Player player,
        int count,
        CardPilePosition position,
        bool forceExhaust = false)
    {
        foreach (var card in MoveCardsForAutoPlay(player, count, position))
        {
            if (State.GetCreature(card.Preview.Owner.Creature).IsDead)
            {
                break;
            }

            card.MutablePreview.ExhaustOnNextPlay = forceExhaust;
            AutoPlay(card);
        }
    }

    // Mirrors CardPileCmd.AutoPlayFromDrawPile until the card is moved to the play pile.
    private IReadOnlyList<PredictedCard> MoveCardsForAutoPlay(Player player, int count, CardPilePosition position)
    {
        var source = _sourceStack.Current;
        var cards = new List<PredictedCard>(count);
        var playerCombatState = State.GetPlayerCombatState(player);
        var drawPile = playerCombatState.DrawPile;

        for (var i = 0; i < count; i++)
        {
            ShuffleIfNecessary(player);
            var card = position switch
            {
                CardPilePosition.Top => drawPile.TopCard,
                CardPilePosition.Bottom => drawPile.BottomCard,
                CardPilePosition.Random => Rng.CombatCardSelection.NextItem(drawPile.Cards),
                _ => null
            };

            if (card is null)
            {
                break;
            }

            cards.Add(card);
            AddToPile(card, playerCombatState.PlayPile);
            History.AutoPlayFromDrawPile(card, source);
        }

        return cards;
    }

    // Mirrors the logic in CardCmd.AutoPlay for resolving a target when none is provided.
    private bool TryResolveAutoPlayTarget(PredictedCard card, ref Creature? target)
    {
        switch (card.Preview.TargetType)
        {
            case TargetType.AnyEnemy:
                target ??= Rng.CombatTargets.NextItem(State.HittableEnemies);
                return target != null;

            case TargetType.AnyAlly:
                target ??= Rng.CombatTargets.NextItem(State.Allies.Where(ally =>
                    ally.IsPlayer && ally != card.Preview.Owner.Creature && State.GetCreature(ally).IsAlive));
                return target != null;

            default:
                return true;
        }
    }
}
