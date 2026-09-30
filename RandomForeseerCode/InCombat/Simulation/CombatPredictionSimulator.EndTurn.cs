using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.InCombat.Mirrors;
using RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Cards;

namespace RandomForeseer.RandomForeseerCode.InCombat.Simulation;

internal sealed partial class CombatPredictionSimulator
{
    /// <summary>
    /// Mirrors the prediction-relevant parts of the player turn's phase-one and phase-two end flow.
    /// </summary>
    public void SimulateEndPlayerTurn()
    {
        var playersEndingTurn = CombatManager.Instance.PlayersTakingExtraTurn switch
        {
            { Count: > 0 } extraTurnPlayers => extraTurnPlayers,
            _ => State.CombatState.Players
        };

        SimulateEndPlayerTurnPhaseOne(playersEndingTurn);

        if (!IsInProgress)
        {
            return;
        }

        SimulateEndPlayerTurnPhaseTwo(playersEndingTurn);
    }

    /// <summary>
    /// Mirrors the prediction-relevant parts of <see cref="CombatManager.EndPlayerTurnPhaseOneInternal()"/>.
    /// </summary>
    private void SimulateEndPlayerTurnPhaseOne(IReadOnlyList<Player> playersEndingTurn)
    {
        foreach (var player in playersEndingTurn)
        {
            HookMirrors.AfterAutoPostPlayPhaseEntered(this, player);
        }

        HookMirrors.BeforeSideTurnEnd(
            this,
            State.CombatState.CurrentSide,
            [.. playersEndingTurn.Select(static player => player.Creature)]);

        if (CheckWinCondition())
        {
            return;
        }

        foreach (var player in playersEndingTurn)
        {
            DoTurnEnd(player);
        }

        if (CheckWinCondition())
        {
            return;
        }

        foreach (var player in playersEndingTurn)
        {
            HookMirrors.BeforeFlush(this, player);
        }

        // Vanilla checks for combat end after the BeforeFlush callbacks before phase two begins.
        CheckWinCondition();
    }

    /// <summary>
    /// Mirrors the prediction-relevant parts of <see cref="CombatManager.EndPlayerTurnPhaseTwoInternal()"/>.
    /// </summary>
    private void SimulateEndPlayerTurnPhaseTwo(IReadOnlyList<Player> playersEndingTurn)
    {
        foreach (var player in playersEndingTurn)
        {
            FlushPlayerHand(player);
        }

        HookMirrors.AfterSideTurnEnd(
            this,
            State.CombatState.CurrentSide,
            [.. playersEndingTurn.Select(static player => player.Creature)]);
    }

    /// <summary>
    /// Mirrors the prediction-relevant parts of <see cref="CombatManager.FlushPlayerHand"/>.
    /// </summary>
    private void FlushPlayerHand(Player player)
    {
        if (!State.GetCreature(player.Creature).IsAlive || player.PlayerCombatState is null)
        {
            return;
        }

        var playerState = State.GetPlayerCombatState(player);
        var shouldFlush = HookMirrors.ShouldFlush(this, player);
        List<PredictedCard> cardsToFlush = [];
        List<PredictedCard> cardsToRetain = [];

        foreach (var card in playerState.Hand.Cards.ToList())
        {
            if (!shouldFlush ||
                card.GetKeywords(this).Contains(CardKeyword.Retain) ||
                card.Preview._hasSingleTurnRetain)
            {
                cardsToRetain.Add(card);
            }
            else
            {
                cardsToFlush.Add(card);
            }
        }

        if (cardsToFlush.Count > 0)
        {
            AddToPile(cardsToFlush, playerState.DiscardPile);
        }

        HookMirrors.AfterFlush(this, player, cardsToFlush, cardsToRetain);
        // Skip EndOfTurnCleanup to avoid cloning every combat card on each prediction refresh.
        // Rare deferred-draw chains can retain turn-local costs/flags; see docs/hooks/end-turn-hooks.md.
    }

    /// <summary>
    /// Mirrors the prediction-relevant parts of <see cref="CombatManager.DoTurnEnd"/>.
    /// </summary>
    private void DoTurnEnd(Player player)
    {
        var playerState = State.GetPlayerCombatState(player);
        playerState.OrbQueue.BeforeTurnEnd(this);

        if (IsOverOrEnding)
        {
            return;
        }

        List<PredictedCard> turnEndCards = [];
        List<PredictedCard> etherealCards = [];

        foreach (var card in playerState.Hand.Cards)
        {
            if (card.Preview.HasTurnEndInHandEffect)
            {
                turnEndCards.Add(card);
            }
            else if (card.GetKeywords(this).Contains(CardKeyword.Ethereal) &&
                     HookMirrors.ShouldEtherealTrigger(this, card.Preview))
            {
                etherealCards.Add(card);
            }
        }

        foreach (var card in etherealCards)
        {
            Exhaust(card, causedByEthereal: true);
        }

        DoTurnEndCards(turnEndCards);
    }

    /// <summary>
    /// Mirrors the prediction-relevant parts of <see cref="CombatManager.DoTurnEndCards"/>.
    /// </summary>
    private void DoTurnEndCards(IEnumerable<PredictedCard> cards)
    {
        foreach (var card in cards)
        {
            AddToPile(card, PileType.Play);
            CardOnTurnEndInHandMirrors.Invoke(this, card);

            // Vanilla does not check Hook.ShouldEtherealTrigger here, so we keep the same behavior.
            if (card.GetKeywords(this).Contains(CardKeyword.Ethereal))
            {
                Exhaust(card, causedByEthereal: true);
            }
            else
            {
                AddToPile(card, PileType.Discard);
            }
        }
    }
}
