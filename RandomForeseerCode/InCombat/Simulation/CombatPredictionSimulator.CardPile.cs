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

        count = Math.Min(count, MaxSimulatedDraws);
        var state = State.GetPlayerCombatState(player);
        var predictedCards = new List<PredictedCard>();

        for (var i = 0; i < count; i++)
        {
            if (state.DrawPileCards.Count == 0)
            {
                if (state.DiscardPileCards.Count == 0)
                {
                    break;
                }

                ShuffleIfNecessary(player);
            }

            if (state.DrawPileCards.Count == 0)
            {
                break;
            }

            var card = state.DrawPileCards[0];
            state.DrawPileCards.RemoveAt(0);
            predictedCards.Add(card);
        }

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
        if (state.DiscardPileCards.Count == 0)
        {
            return DrawPilePredictionResult.Empty;
        }

        state.DrawPileCards.Clear();
        Shuffle(player);
        return DrawPilePredictionResult.FromPredictedCards(state.DrawPileCards, Snapshot());
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

        for (var i = 0; i < drawCount; i++)
        {
            if (State.GetPlayerCombatState(player).HandCards.Count >= CardPile.MaxCardsInHand || !DrawOne(player))
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
        if (state.DrawPileCards.Count == 0)
        {
            return false;
        }

        var predictedCard = state.DrawPileCards[0];
        state.DrawPileCards.RemoveAt(0);
        state.HandCards.Add(predictedCard);
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
        state.DrawPileCards.AddRange(state.HandCards);
        state.HandCards.Clear();
    }

    public void RemoveFromHand(Player player, CardModel card)
    {
        State.GetPlayerCombatState(player).HandCards.RemoveAll(predictedCard => predictedCard.Original == card);
    }

    public int DiscardHand(Player player)
    {
        var state = State.GetPlayerCombatState(player);
        var count = state.HandCards.Count;
        foreach (var card in state.HandCards.ToList())
        {
            state.HandCards.Remove(card);
            state.DiscardPileCards.Add(card);
            RunAfterCardDiscardedHooks(card);
        }

        return count;
    }

    public void ExhaustHand(Player player)
    {
        var state = State.GetPlayerCombatState(player);
        var exhaustedCards = state.HandCards.ToList();
        foreach (var card in exhaustedCards)
        {
            state.HandCards.Remove(card);
            state.ExhaustPileCards.Add(card);
            RunAfterCardExhaustedHooks(player, card, causedByEthereal: false);
        }
    }

    public DrawPilePredictionResult RandomizeHandCosts(Player player)
    {
        var state = State.GetPlayerCombatState(player);
        foreach (var card in state.HandCards)
        {
            if (card.Preview.EnergyCost.CostsX ||
                card.Preview.EnergyCost.GetWithModifiers(CostModifiers.None) < 0)
            {
                continue;
            }

            card.MutablePreview.EnergyCost.SetThisTurnOrUntilPlayed(Rng.CombatEnergyCosts.NextInt(4));
        }

        return DrawPilePredictionResult.FromPredictedCards(state.HandCards, Snapshot());
    }

    public void Shuffle(Player player)
    {
        // Mirrors CardPileCmd.Shuffle: merge discard cards with current draw-pile cards,
        // shuffle the combined list, then place all cards back into the draw pile.
        var state = State.GetPlayerCombatState(player);
        var shuffledCards = state.DiscardPileCards.ToList();

        // The original code adds draw-pile cards through ToHashSet(), relying on the current
        // implementation's iteration order; card piles do not contain duplicates, so the preview
        // uses the source order directly instead of modeling that implementation detail.
        shuffledCards.AddRange(state.DrawPileCards);
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

        state.DrawPileCards.Clear();
        state.DrawPileCards.AddRange(shuffledCards);
        state.DiscardPileCards.Clear();
    }

    private void ShuffleIfNecessary(Player player)
    {
        var state = State.GetPlayerCombatState(player);
        if (state.DrawPileCards.Count > 0 || state.DiscardPileCards.Count == 0)
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
        if (state.HandCards.Count < CardPile.MaxCardsInHand)
        {
            state.HandCards.Add(card);
            _predictedCards.Add(card);
        }
        else
        {
            state.DiscardPileCards.Add(card);
        }
    }
}
