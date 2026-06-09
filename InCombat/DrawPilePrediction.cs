using System.Diagnostics.CodeAnalysis;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Afflictions;
using MegaCrit.Sts2.Core.Random;
using RandomForeseer.Common;
using RandomForeseer.Common.Hooks;
using RandomForeseer.InCombat.Hooks;

namespace RandomForeseer.InCombat;

internal sealed class DrawPilePrediction(
    ICombatState combatState,
    Player player,
    IEnumerable<CardModel> handCards,
    IEnumerable<CardModel> drawPileCards,
    IEnumerable<CardModel> discardPileCards,
    Rng shuffleRng,
    Rng energyCostRng)
{
    private const int MaxSimulatedDraws = 10;

    private readonly List<PredictedCard> _handCards = PredictedCard.FromCards(handCards);
    private readonly List<PredictedCard> _drawPileCards = PredictedCard.FromCards(drawPileCards);
    private readonly List<PredictedCard> _discardPileCards = PredictedCard.FromCards(discardPileCards);
    private readonly List<PredictedCard> _predictedCards = [];

    private readonly DrawPilePredictionState _state = new()
    {
        StatusCardsDrawnThisTurn = CountStatusCardsDrawnThisTurn(combatState, player),
        BoundCardsAfflictedThisTurn = CountBoundCardsAfflictedThisTurn(combatState, player)
    };
    private readonly PredictionStateStore _stateStore = new();
    private readonly PredictionRiskTracker _driftRisk = new();

    private bool _reachedSimulationLimit;

    public static DrawPilePredictionResult PredictTopCardsAfterNecessaryShuffles(Player player, int count)
    {
        return TryCreate(player, out var prediction)
            ? prediction.PeekTopCardsAfterNecessaryShuffles(count)
            : DrawPilePredictionResult.Empty;
    }

    public static DrawPilePredictionResult PredictDraw(Player player, int count)
    {
        return TryCreate(player, out var prediction)
            ? prediction.Draw(count)
            : DrawPilePredictionResult.Empty;
    }

    public static DrawPilePredictionResult PredictShuffleAfterDrawPileDepleted(Player player)
    {
        return TryCreate(player, out var prediction)
            ? prediction.ShuffleAfterDrawPileDepleted()
            : DrawPilePredictionResult.Empty;
    }

    public DrawPilePredictionResult PeekTopCardsAfterNecessaryShuffles(int count)
    {
        if (count <= 0)
        {
            return DrawPilePredictionResult.Empty;
        }

        count = Math.Min(count, MaxSimulatedDraws);

        var predictedCards = new List<PredictedCard>();

        for (var i = 0; i < count; i++)
        {
            if (_drawPileCards.Count == 0)
            {
                if (_discardPileCards.Count == 0)
                {
                    break;
                }

                ShuffleIfNecessary();
            }

            if (_drawPileCards.Count == 0)
            {
                break;
            }

            var card = _drawPileCards[0];
            _drawPileCards.RemoveAt(0);
            predictedCards.Add(card);
        }

        return DrawPilePredictionResult.FromPredictedCards(predictedCards, _driftRisk.Snapshot());
    }

    public DrawPilePredictionResult Draw(int count)
    {
        if (count <= 0)
        {
            return DrawPilePredictionResult.Empty;
        }

        DrawInternal(count);
        return DrawPilePredictionResult.FromPredictedCards(_predictedCards, _driftRisk.Snapshot());
    }

    public DrawPilePredictionResult ShuffleAfterDrawPileDepleted()
    {
        if (_discardPileCards.Count == 0)
        {
            return DrawPilePredictionResult.Empty;
        }

        _drawPileCards.Clear();
        Shuffle();
        return DrawPilePredictionResult.FromPredictedCards(_drawPileCards, _driftRisk.Snapshot());
    }

    private void DrawInternal(int drawCount)
    {
        if (drawCount <= 0 || _reachedSimulationLimit)
        {
            return;
        }

        var shouldDrawContext = new ShouldDrawHookContext
        {
            RiskTracker = _driftRisk,
            CombatState = combatState,
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
            if (_handCards.Count >= CardPile.MaxCardsInHand || !DrawOne())
            {
                break;
            }
        }
    }

    private bool DrawOne()
    {
        if (_predictedCards.Count >= MaxSimulatedDraws)
        {
            _driftRisk.AddUnknown();
            _reachedSimulationLimit = true;
            return false;
        }

        ShuffleIfNecessary();

        if (_drawPileCards.Count == 0)
        {
            return false;
        }

        var predictedCard = _drawPileCards[0];
        _drawPileCards.RemoveAt(0);
        _handCards.Add(predictedCard);
        _predictedCards.Add(predictedCard);

        if (predictedCard.Original.Type == CardType.Status)
        {
            _state.StatusCardsDrawnThisTurn++;
        }

        RunAfterCardDrawnHooks(predictedCard, DrawInternal);
        return true;
    }

    public void MoveHandToDrawPile()
    {
        _drawPileCards.AddRange(_handCards);
        _handCards.Clear();
    }

    public void RemoveFromHand(CardModel card)
    {
        _handCards.RemoveAll(predictedCard => predictedCard.Original == card);
    }

    public int DiscardHand()
    {
        var count = _handCards.Count;
        foreach (var card in _handCards.ToList())
        {
            _handCards.Remove(card);
            _discardPileCards.Add(card);
            RunAfterCardDiscardedHooks(card);
        }

        return count;
    }

    public void ExhaustHand()
    {
        var exhaustedCards = _handCards.ToList();
        foreach (var card in exhaustedCards)
        {
            _handCards.Remove(card);
            RunAfterCardExhaustedHooks(card, causedByEthereal: false);
        }
    }

    public DrawPilePredictionResult RandomizeHandCosts()
    {
        foreach (var card in _handCards)
        {
            if (card.Preview.EnergyCost.CostsX ||
                card.Preview.EnergyCost.GetWithModifiers(CostModifiers.None) < 0)
            {
                continue;
            }

            card.MutablePreview.EnergyCost.SetThisTurnOrUntilPlayed(energyCostRng.NextInt(4));
        }

        return DrawPilePredictionResult.FromPredictedCards(_handCards, _driftRisk.Snapshot());
    }

    public void Shuffle()
    {
        // Mirrors CardPileCmd.Shuffle: merge discard cards with current draw-pile cards,
        // shuffle the combined list, then place all cards back into the draw pile.

        // The original code adds draw-pile cards through ToHashSet(), relying on the current
        // implementation's iteration order; card piles do not contain duplicates, so the preview
        // uses the source order directly instead of modeling that implementation detail.
        var shuffledCards = _discardPileCards.ToList();
        shuffledCards.AddRange(_drawPileCards);
        shuffledCards.StableShuffle(shuffleRng);

        var originalShuffledCards = shuffledCards.Select(card => card.Original).ToList();
        Hook.ModifyShuffleOrder(combatState, player, originalShuffledCards, isInitialShuffle: false);
        var shuffledCardsByOriginal = shuffledCards.ToDictionary(card => card.Original);
        shuffledCards = originalShuffledCards
            .Select(card => shuffledCardsByOriginal.TryGetValue(card, out var predictedCard)
                ? predictedCard
                : new PredictedCard(card))
            .ToList();

        AfterShuffleHook.Run(new AfterShuffleHookContext
        {
            RiskTracker = _driftRisk,
            CombatState = combatState,
            Player = player,
            DrawPileCards = shuffledCards,
            ShuffleRng = shuffleRng
        });

        _drawPileCards.Clear();
        _drawPileCards.AddRange(shuffledCards);
        _discardPileCards.Clear();
    }

    public static bool TryCreate(Player player, [NotNullWhen(true)] out DrawPilePrediction? prediction)
    {
        if (player.Creature.CombatState is not { } combatState)
        {
            prediction = null;
            return false;
        }

        prediction = new DrawPilePrediction(
            combatState,
            player,
            PileType.Hand.GetPile(player).Cards,
            PileType.Draw.GetPile(player).Cards,
            PileType.Discard.GetPile(player).Cards,
            PredictionUtils.CloneRng(player.RunState.Rng.Shuffle),
            PredictionUtils.CloneRng(player.RunState.Rng.CombatEnergyCosts));
        return true;
    }

    private void ShuffleIfNecessary()
    {
        if (_drawPileCards.Count > 0 || _discardPileCards.Count == 0)
        {
            return;
        }

        Shuffle();
    }

    private void RunAfterCardDrawnHooks(PredictedCard card, Action<int> draw)
    {
        var context = new AfterCardDrawnHookContext
        {
            RiskTracker = _driftRisk,
            CombatState = combatState,
            Player = player,
            Card = card,
            FromHandDraw = false,
            EnergyCostRng = energyCostRng,
            State = _state,
            Draw = draw
        };

        AfterCardDrawnHook.RunEarly(context);
        AfterCardDrawnHook.Run(context);
    }

    private void RunAfterCardDiscardedHooks(PredictedCard card)
    {
        var context = new AfterCardDiscardedHookContext
        {
            RiskTracker = _driftRisk,
            CombatState = combatState,
            Card = card
        };

        AfterCardDiscardedHook.Run(context);
    }

    private void RunAfterCardExhaustedHooks(PredictedCard card, bool causedByEthereal)
    {
        var context = new AfterCardExhaustedHookContext
        {
            RiskTracker = _driftRisk,
            CombatState = combatState,
            Player = player,
            Card = card,
            CausedByEthereal = causedByEthereal,
            StateStore = _stateStore,
            Draw = DrawInternal,
            AddToHand = AddToHand
        };

        AfterCardExhaustedHook.Run(context);
    }

    private void AddToHand(PredictedCard card)
    {
        if (_handCards.Count < CardPile.MaxCardsInHand)
        {
            _handCards.Add(card);
            _predictedCards.Add(card);
        }
    }

    private static int CountStatusCardsDrawnThisTurn(ICombatState combatState, Player player)
    {
        return CombatManager.Instance.History.Entries
            .OfType<CardDrawnEntry>()
            .Count(entry =>
                entry.HappenedThisTurn(combatState) &&
                entry.Actor == player.Creature &&
                entry.Card.Type == CardType.Status);
    }

    private static int CountBoundCardsAfflictedThisTurn(ICombatState combatState, Player player)
    {
        return CombatManager.Instance.History.Entries
            .OfType<CardAfflictedEntry>()
            .Count(entry =>
                entry.HappenedThisTurn(combatState) &&
                entry.Actor == player.Creature &&
                entry.Affliction is Bound);
    }

}

internal sealed class DrawPilePredictionState
{
    public int StatusCardsDrawnThisTurn { get; set; }

    public int BoundCardsAfflictedThisTurn { get; set; }
}

internal sealed record DrawPilePredictionResult(IReadOnlyList<CardModel> Cards, PredictionRisk Risk)
{
    public bool HasDriftRisk => Risk.HasRisk;

    public static DrawPilePredictionResult Empty { get; } = new([], PredictionRisk.None);

    public static DrawPilePredictionResult FromPredictedCards(IEnumerable<PredictedCard> cards, PredictionRisk risk)
    {
        return new DrawPilePredictionResult(cards.Select(card => card.Preview).ToList(), risk);
    }

    public IReadOnlyList<IHoverTip> ToHoverTips()
    {
        var tips = PredictionHoverTips.Cards(Cards).ToList();
        if (HasDriftRisk && RandomForeseerSettings.EnableDriftWarnings)
        {
            tips.Add(PredictionHoverTips.DriftWarning("draw_pile", Risk));
        }

        return tips;
    }
}
