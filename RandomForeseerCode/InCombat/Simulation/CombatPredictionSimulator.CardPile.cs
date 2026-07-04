using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.Models;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.InCombat.Hooks;

namespace RandomForeseer.RandomForeseerCode.InCombat.Simulation;

internal sealed partial class CombatPredictionSimulator
{
    private const int MaxSimulatedDraws = 10;

    private readonly List<PredictedCard> _predictedCards = [];

    private bool _reachedSimulationLimit;

    public DrawPilePredictionResult PeekTopCardsAfterNecessaryShuffles(Player player, int count)
    {
        if (count <= 0)
        {
            return DrawPilePredictionResult.Empty;
        }

        if (count > MaxSimulatedDraws)
        {
            count = MaxSimulatedDraws;
            _riskTracker.AddUnknown();
        }

        var predictedCards = MoveCardsForAutoPlay(player, count, CardPilePosition.Top);
        return DrawPilePredictionResult.FromPredictedCards(predictedCards, Snapshot());
    }

    public DrawPilePredictionResult Draw(Player player, int count)
    {
        if (count <= 0)
        {
            return DrawPilePredictionResult.Empty;
        }

        DrawInternal(player, count);
        return DrawPilePredictionResult.FromPredictedCards(_predictedCards, Snapshot());
    }

    public DrawPilePredictionResult ShuffleAfterDrawPileDepleted(Player player)
    {
        var state = State.GetPlayerCombatState(player);
        if (state.DiscardPile.IsEmpty)
        {
            return DrawPilePredictionResult.Empty;
        }

        state.DrawPile.Clear();
        Shuffle(player);
        return DrawPilePredictionResult.FromPredictedCards(state.DrawPile.Cards, Snapshot());
    }

    private void DrawInternal(Player player, int drawCount)
    {
        if (drawCount <= 0 || _reachedSimulationLimit)
        {
            return;
        }

        var shouldDrawContext = new ShouldDrawHookContext
        {
            Simulator = this,
            Player = player,
            FromHandDraw = false
        };
        ShouldDrawHook.Run(shouldDrawContext);

        if (shouldDrawContext.IsBlocked)
        {
            return;
        }

        var hand = State.GetPlayerCombatState(player).Hand;

        for (var i = 0; i < drawCount; i++)
        {
            if (hand.Cards.Count >= CardPile.MaxCardsInHand || !DrawOne(player))
            {
                break;
            }
        }
    }

    private bool DrawOne(Player player)
    {
        if (_predictedCards.Count >= MaxSimulatedDraws)
        {
            _riskTracker.AddUnknown();
            _reachedSimulationLimit = true;
            return false;
        }

        ShuffleIfNecessary(player);

        var state = State.GetPlayerCombatState(player);
        if (state.DrawPile.IsEmpty)
        {
            return false;
        }

        var predictedCard = state.DrawPile.Cards[0];
        state.DrawPile.Remove(predictedCard);
        state.Hand.Add(predictedCard);
        _predictedCards.Add(predictedCard);
        RecordCardDrawnHistory(player, predictedCard);

        if (predictedCard.Original.Type == CardType.Status)
        {
            state.CardDrawState.StatusCardsDrawnThisTurn++;
        }

        RunAfterCardDrawnHooks(player, predictedCard);
        return true;
    }

    public void MoveHandToDrawPile(Player player)
    {
        var state = State.GetPlayerCombatState(player);
        state.DrawPile.AddRange(state.Hand.Cards);
        state.Hand.Clear();
    }

    // TODO: Use something like AddCardToPile(card, PileType.Play) instead
    public void RemoveFromHand(CardModel card)
    {
        var hand = State.GetPlayerCombatState(card.Owner).Hand;
        if (hand.Find(card) is { } predictedCard)
        {
            hand.Remove(predictedCard);
        }
    }

    public int DiscardHand(Player player)
    {
        var state = State.GetPlayerCombatState(player);
        var cards = state.Hand.Cards.ToList();
        foreach (var card in cards)
        {
            state.Hand.Remove(card);
            state.DiscardPile.Add(card);
            RunAfterCardDiscardedHooks(card);
        }

        return cards.Count;
    }

    public void ExhaustHand(Player player)
    {
        var state = State.GetPlayerCombatState(player);
        var cards = state.Hand.Cards.ToList();
        foreach (var card in cards)
        {
            state.Hand.Remove(card);
            state.ExhaustPile.Add(card);
            RunAfterCardExhaustedHooks(player, card, causedByEthereal: false);
        }
    }

    public DrawPilePredictionResult RandomizeHandCosts(Player player)
    {
        var state = State.GetPlayerCombatState(player);
        foreach (var card in state.Hand.Cards)
        {
            if (card.Preview.EnergyCost.CostsX ||
                card.Preview.EnergyCost.GetWithModifiers(CostModifiers.None) < 0)
            {
                continue;
            }

            card.MutablePreview.EnergyCost.SetThisTurnOrUntilPlayed(Rng.CombatEnergyCosts.NextInt(4));
        }

        return DrawPilePredictionResult.FromPredictedCards(state.Hand.Cards, Snapshot());
    }

    public void Shuffle(Player player)
    {
        // Mirrors CardPileCmd.Shuffle: merge discard cards with current draw-pile cards,
        // shuffle the combined list, then place all cards back into the draw pile.
        var state = State.GetPlayerCombatState(player);
        var shuffledCards = state.DiscardPile.Cards.ToList();

        // The original code adds draw-pile cards through ToHashSet(), relying on the current
        // implementation's iteration order; card piles do not contain duplicates, so the preview
        // uses the source order directly instead of modeling that implementation detail.
        shuffledCards.AddRange(state.DrawPile.Cards);
        shuffledCards.StableShuffle(Rng.Shuffle);

        ShuffleHooks.RunModifyShuffleOrder(new ModifyShuffleOrderHookContext
        {
            Simulator = this,
            Player = player,
            DrawPileCards = shuffledCards,
            IsInitialShuffle = false
        });

        ShuffleHooks.RunAfterShuffle(new AfterShuffleHookContext
        {
            Simulator = this,
            Player = player,
            DrawPileCards = shuffledCards
        });

        state.DrawPile.Clear();
        state.DrawPile.AddRange(shuffledCards);
        state.DiscardPile.Clear();
    }

    private void ShuffleIfNecessary(Player player)
    {
        var state = State.GetPlayerCombatState(player);
        if (!state.DrawPile.IsEmpty || state.DiscardPile.IsEmpty)
        {
            return;
        }

        Shuffle(player);
    }

    private void RunAfterCardDrawnHooks(Player player, PredictedCard card)
    {
        var context = new AfterCardDrawnHookContext
        {
            Simulator = this,
            Player = player,
            Card = card,
            FromHandDraw = false
        };

        AfterCardDrawnHook.RunEarly(context);
        AfterCardDrawnHook.Run(context);
    }

    private void RunAfterCardDiscardedHooks(PredictedCard card)
    {
        var context = new AfterCardDiscardedHookContext
        {
            Simulator = this,
            Card = card
        };

        AfterCardDiscardedHook.Run(context);
    }

    private void RunAfterCardExhaustedHooks(Player player, PredictedCard card, bool causedByEthereal)
    {
        var context = new AfterCardExhaustedHookContext
        {
            Simulator = this,
            Player = player,
            Card = card,
            CausedByEthereal = causedByEthereal
        };

        AfterCardExhaustedHook.Run(context);
    }

    public void AddToHand(Player player, PredictedCard card)
    {
        var state = State.GetPlayerCombatState(player);
        if (state.Hand.Cards.Count < CardPile.MaxCardsInHand)
        {
            state.Hand.Add(card);
            _predictedCards.Add(card);
        }
        else
        {
            state.DiscardPile.Add(card);
        }
    }
}
