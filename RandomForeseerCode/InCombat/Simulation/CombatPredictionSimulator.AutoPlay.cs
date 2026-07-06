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
        bool skipXCapture = false,
        OnPlayDelegate? onPlay = null)
    {
        if (card.Preview.Keywords.Contains(CardKeyword.Unplayable) ||
            !Hook.ShouldPlay(combatState, card.Preview, out var _, type) ||
            !TryResolveAutoPlayTarget(card, ref target))
        {
            MoveToResultPileWithoutPlaying(card);
            return;
        }

        var playerCombatState = State.GetPlayerCombatState(card.Preview.Owner);

        if (!skipXCapture)
        {
            if (card.Preview.EnergyCost.CostsX)
            {
                card.MutablePreview.EnergyCost.CapturedXValue = playerCombatState.Energy;
            }

            card.MutablePreview.LastStarsSpent = card.Preview.HasStarCostX
                ? playerCombatState.Stars
                : Math.Max(0, card.Preview.GetStarCostWithModifiers());
        }

        if (card.GetPile(State) is null)
        {
            AddToPile(card, PileType.Play);
        }

        // TODO: Dispatch Hook.BeforeCardAutoPlayed
        // TODO: Simulate SpendResources and pass the result to OnPlayWrapper.
        OnPlayWrapper(card, target, isAutoPlay: true, onPlay ?? ((_, _) => MarkCurrentSourceRisky()));
    }

    // Mirrors CardPileCmd.AutoPlayFromDrawPile through selecting cards and moving them to
    // the play pile. Actual card autoplay is still a risk marker in AutoPlay.
    public void AutoPlayFromDrawPile(
        Player player,
        int count,
        CardPilePosition position,
        bool forceExhaust = false,
        OnPlayDelegate? onPlay = null)
    {
        foreach (var card in MoveCardsForAutoPlay(player, count, position))
        {
            if (State.GetCreature(card.Original.Owner.Creature).IsDead)
            {
                break;
            }

            card.MutablePreview.ExhaustOnNextPlay = forceExhaust;
            AutoPlay(card, onPlay: onPlay);
        }
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
