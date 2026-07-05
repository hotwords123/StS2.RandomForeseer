using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
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

    // Mirrors CardPileCmd.AddToCombatAndPreview<T>(Creature, PileType, int, Player?, CardPilePosition)
    // up to card creation and AddGeneratedCardToCombat dispatch. Preview animation is UI-only.
    public void AddToCombat<TCard>(
        Creature target,
        PileType pileType,
        int count,
        Player? creator,
        CardPilePosition position = CardPilePosition.Bottom)
        where TCard : CardModel
    {
        var player = target.Player ?? target.PetOwner;
        if (player is null || State.GetCreature(player.Creature).IsDead)
        {
            return;
        }

        List<PredictedCard> cards = [];
        for (var i = 0; i < count; i++)
        {
            cards.Add(PredictedCard.Create(ModelDb.Card<TCard>(), player));
        }

        AddGeneratedCardsToCombat(cards, pileType, creator, position);
    }

    // Convenience overload for AddGeneratedCardsToCombat with a single card.
    public SimCardPileAddResult AddGeneratedCardToCombat(
        PredictedCard card,
        PileType newPileType,
        Player? creator,
        CardPilePosition position = CardPilePosition.Bottom)
    {
        return AddGeneratedCardsToCombat([card], newPileType, creator, position)[0];
    }

    // Mirrors CardPileCmd.AddGeneratedCardsToCombat for combat piles.
    public IReadOnlyList<SimCardPileAddResult> AddGeneratedCardsToCombat(
        IReadOnlyList<PredictedCard> cards,
        PileType newPileType,
        Player? creator,
        CardPilePosition position = CardPilePosition.Bottom)
    {
        if (cards.Count == 0)
        {
            return [];
        }

        if (!newPileType.IsCombatPile())
        {
            throw new InvalidOperationException("Generated combat cards can only be added to combat piles.");
        }

        if (cards.Any(card => card.GetPile(State) is not null))
        {
            throw new InvalidOperationException("Generated combat cards cannot already be in a pile.");
        }

        List<SimCardPileAddResult> results = [];

        foreach (var card in cards)
        {
            // Vanilla records CombatManager.Instance.History.CardGenerated here. The simulator
            // does not currently consume generated-card history, so this is intentionally omitted.

            results.Add(AddToPile(card, newPileType, position));

            AfterCardGeneratedForCombatHook.Run(new AfterCardGeneratedForCombatHookContext
            {
                Simulator = this,
                Card = card,
                Creator = creator
            });
        }

        return results;
    }

    // Convenience overload for AddToPile with a single card.
    public SimCardPileAddResult AddToPile(
        PredictedCard card,
        PileType newPileType,
        CardPilePosition position = CardPilePosition.Bottom)
    {
        return AddToPile([card], newPileType, position)[0];
    }

    // Mirrors the combat-pile branch of CardPileCmd.Add(IEnumerable<CardModel>, CardPile, ...).
    public IReadOnlyList<SimCardPileAddResult> AddToPile(
        IReadOnlyList<PredictedCard> cards,
        PileType newPileType,
        CardPilePosition position = CardPilePosition.Bottom)
    {
        if (!newPileType.IsCombatPile())
        {
            throw new InvalidOperationException($"Cannot add card to non-combat pile {newPileType}.");
        }

        if (cards.Count == 0)
        {
            return [];
        }

        var owner = cards[0].Preview.Owner
            ?? throw new InvalidOperationException($"Cannot add cards with no owner to a pile.");
        var playerCombatState = State.GetPlayerCombatState(owner);

        List<SimCardPileAddResult> results = [];

        foreach (var card in cards)
        {
            if (card.Preview.Owner != owner)
            {
                throw new InvalidOperationException("Cannot add cards with different owners to the same pile.");
            }

            var oldPile = card.GetPile(playerCombatState);
            var oldPileType = oldPile?.Type ?? PileType.None;

            if (card.Original.HasBeenRemovedFromState ||
                card.Preview.HasBeenRemovedFromState ||
                State.GetCreature(owner.Creature).IsDead ||
                (oldPileType != PileType.None && !oldPileType.IsCombatPile()))
            {
                results.Add(new SimCardPileAddResult(false, card, oldPileType));
                continue;
            }

            // Vanilla checks for card.UpgradePreviewType.IsPreview() here and throws if true.
            // The simulator does not currently support preview cards, so this is intentionally omitted.

            results.Add(new SimCardPileAddResult(true, card, oldPileType));
        }

        foreach (var result in results)
        {
            if (!result.Success)
            {
                continue;
            }

            var card = result.CardAdded;

            var targetPile = playerCombatState.GetCardPile(newPileType)
                ?? throw new InvalidOperationException($"Cannot find combat pile {newPileType} for player {owner}.");
            if (targetPile.Type == PileType.Hand && targetPile.Cards.Count >= CardPile.MaxCardsInHand)
            {
                targetPile = playerCombatState.DiscardPile;
            }

            card.GetPile(playerCombatState)?.Remove(card);

            var index = position switch
            {
                CardPilePosition.Bottom => targetPile.Cards.Count,
                CardPilePosition.Top => 0,
                CardPilePosition.Random => Rng.Shuffle.NextInt(targetPile.Cards.Count + 1),
                _ => throw new ArgumentOutOfRangeException(nameof(position), position, null)
            };
            targetPile.Insert(index, card);

            // Vanilla CardPile.AddInternal updates CombatManager.StateTracker and raises pile UI events.
            // Prediction piles are plain model mirrors, and those UI-facing side effects are ignored.

            if (result.OldPileType == PileType.None)
            {
                // Vanilla dispatches Hook.AfterCardEnteredCombat here. Current reviewed vanilla
                // implementations only mutate the entering card, and this is low-impact for current
                // prediction surfaces, so the mirror intentionally skips it for now.
            }
        }

        // Vanilla dispatches Hook.AfterCardChangedPiles after visuals finish. Current vanilla
        // listeners are deck-only or VFX/music-only for combat piles, so this is intentionally
        // skipped until a prediction-relevant combat-pile listener appears.

        return results;
    }
}

internal readonly record struct SimCardPileAddResult(
    bool Success,
    PredictedCard CardAdded,
    PileType OldPileType);
