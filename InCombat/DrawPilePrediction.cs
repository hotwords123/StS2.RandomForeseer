using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
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
    Rng energyCostRng,
    bool hasDriftRisk = false)
{
    private const int MaxSimulatedDraws = 10;

    private readonly List<PredictedCard> _handCards = PredictedCard.FromCards(handCards);
    private readonly List<PredictedCard> _drawPileCards = PredictedCard.FromCards(drawPileCards);
    private readonly List<PredictedCard> _discardPileCards = PredictedCard.FromCards(discardPileCards);
    private readonly List<PredictedCard> _predictedCards = [];

    private readonly StrongBox<int> _statusCardsDrawnThisTurn = new(CountStatusCardsDrawnThisTurn(combatState, player));
    private readonly StrongBox<int> _boundCardsAfflictedThisTurn = new(CountBoundCardsAfflictedThisTurn(combatState, player));
    private readonly Dictionary<RelicModel, int> _jossPaperCardsExhausted = [];
    private readonly Dictionary<RelicModel, bool> _burningSticksUsedThisCombat = [];

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

        return DrawPilePredictionResult.FromPredictedCards(predictedCards, hasDriftRisk);
    }

    public DrawPilePredictionResult Draw(int count)
    {
        if (count <= 0)
        {
            return DrawPilePredictionResult.Empty;
        }

        DrawInternal(count);
        return DrawPilePredictionResult.FromPredictedCards(_predictedCards, hasDriftRisk);
    }

    public DrawPilePredictionResult ShuffleAfterDrawPileDepleted()
    {
        if (_discardPileCards.Count == 0)
        {
            return DrawPilePredictionResult.Empty;
        }

        _drawPileCards.Clear();
        Shuffle();
        return DrawPilePredictionResult.FromPredictedCards(_drawPileCards, hasDriftRisk);
    }

    private void DrawInternal(int drawCount)
    {
        if (drawCount <= 0 || _reachedSimulationLimit)
        {
            return;
        }

        var shouldDrawResults = ShouldDrawHook.Run(new ShouldDrawHookContext
        {
            CombatState = combatState,
            Player = player,
            FromHandDraw = false
        });

        hasDriftRisk |= HasRisk(shouldDrawResults);

        if (shouldDrawResults.Any(result => result.Kind == HookResultKind.Blocked))
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
            hasDriftRisk = true;
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
            _statusCardsDrawnThisTurn.Value++;
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
        _discardPileCards.AddRange(_handCards);
        _handCards.Clear();
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

        return DrawPilePredictionResult.FromPredictedCards(_handCards, hasDriftRisk);
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

        var hookResults = AfterShuffleHook.Run(new AfterShuffleHookContext
        {
            CombatState = combatState,
            Player = player,
            DrawPileCards = shuffledCards,
            ShuffleRng = shuffleRng
        });

        _drawPileCards.Clear();
        _drawPileCards.AddRange(shuffledCards);
        _discardPileCards.Clear();
        hasDriftRisk |= HasRisk(hookResults);
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
            CombatState = combatState,
            Player = player,
            Card = card,
            FromHandDraw = false,
            EnergyCostRng = energyCostRng,
            StatusCardsDrawnThisTurn = _statusCardsDrawnThisTurn,
            BoundCardsAfflictedThisTurn = _boundCardsAfflictedThisTurn,
            Draw = draw
        };

        hasDriftRisk |= HasRisk(AfterCardDrawnHook.RunEarly(context));
        hasDriftRisk |= HasRisk(AfterCardDrawnHook.Run(context));
    }

    private void RunAfterCardExhaustedHooks(PredictedCard card, bool causedByEthereal)
    {
        var context = new AfterCardExhaustedHookContext
        {
            CombatState = combatState,
            Player = player,
            Card = card,
            CausedByEthereal = causedByEthereal,
            JossPaperCardsExhausted = _jossPaperCardsExhausted,
            BurningSticksUsedThisCombat = _burningSticksUsedThisCombat,
            Draw = DrawInternal,
            AddToHand = AddToHand
        };

        hasDriftRisk |= HasRisk(AfterCardExhaustedHook.Run(context));
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

    private static bool HasRisk(IEnumerable<HookResult> results)
    {
        return results.Any(result => result.Kind is HookResultKind.DriftRisk or HookResultKind.Unsupported);
    }
}

internal sealed record DrawPilePredictionResult(IReadOnlyList<CardModel> Cards, bool HasDriftRisk)
{
    public static DrawPilePredictionResult Empty { get; } = new([], false);

    public static DrawPilePredictionResult FromPredictedCards(IEnumerable<PredictedCard> cards, bool hasDriftRisk)
    {
        return new DrawPilePredictionResult(cards.Select(card => card.Preview).ToList(), hasDriftRisk);
    }

    public IReadOnlyList<IHoverTip> ToHoverTips()
    {
        var tips = PredictionHoverTips.Cards(Cards).ToList();
        if (HasDriftRisk && RandomForeseerSettings.EnableDriftWarnings)
        {
            tips.Add(PredictionHoverTips.DriftWarning("draw_pile"));
        }

        return tips;
    }
}
