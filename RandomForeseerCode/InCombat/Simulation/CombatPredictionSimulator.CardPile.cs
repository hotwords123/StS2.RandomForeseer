using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.Models;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.InCombat.Mirrors;

namespace RandomForeseer.RandomForeseerCode.InCombat.Simulation;

internal sealed partial class CombatPredictionSimulator
{
    private const int MaxSimulatedDraws = 10;

    // Mirrors CardPileCmd.Draw.
    public void Draw(Player player, int drawCount, bool fromHandDraw = false)
    {
        if (!HookMirrors.ShouldDraw(this, player, fromHandDraw, out _))
        {
            // Vanilla calls Hook.AfterPreventingDraw here, but all current listeners are cosmetic.
            return;
        }

        var hand = State.GetPlayerCombatState(player).Hand;

        for (var i = 0; i < drawCount; i++)
        {
            if (hand.Cards.Count >= CardPile.MaxCardsInHand || !DrawOne(player, fromHandDraw))
            {
                break;
            }
        }
    }

    // Mirrors the body of the draw loop in CardPileCmd.Draw.
    private bool DrawOne(Player player, bool fromHandDraw)
    {
        if (_cardDrawnHistory.Count >= MaxSimulatedDraws)
        {
            _riskTracker.AddUnknown();
            return false;
        }

        ShuffleIfNecessary(player);

        var state = State.GetPlayerCombatState(player);
        if (state.DrawPile.IsEmpty)
        {
            return false;
        }

        var predictedCard = state.DrawPile.Cards[0];
        AddToPile(predictedCard, state.Hand);
        RecordCardDrawnHistory(predictedCard, fromHandDraw);

        HookMirrors.AfterCardDrawn(this, predictedCard, fromHandDraw);
        return true;
    }

    public void Shuffle(Player player)
    {
        // Mirrors CardPileCmd.Shuffle: merge discard cards with current draw-pile cards,
        // shuffle the combined list, then place all cards back into the draw pile.
        var state = State.GetPlayerCombatState(player);
        var drawPileCards = state.DrawPile.Cards.ToHashSet();
        var shuffledCards = state.DiscardPile.Cards.ToList();

        // The original code adds draw-pile cards through ToHashSet(), relying on the current
        // implementation's iteration order; card piles do not contain duplicates, so the preview
        // uses the source order directly instead of modeling that implementation detail.
        shuffledCards.AddRange(state.DrawPile.Cards);
        shuffledCards.StableShuffle(Rng.Shuffle);

        HookMirrors.ModifyShuffleOrder(this, player, shuffledCards, isInitialShuffle: false);

        foreach (var card in drawPileCards)
        {
            state.DrawPile.Remove(card);
        }

        foreach (var card in shuffledCards)
        {
            if (drawPileCards.Contains(card))
            {
                state.DrawPile.Add(card);
            }
            else
            {
                AddToPile(card, state.DrawPile);
            }
        }

        HookMirrors.AfterShuffle(this, player);
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
            HookMirrors.AfterCardGeneratedForCombat(this, card, creator);
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

    public SimCardPileAddResult AddToPile(
        PredictedCard card,
        SimCardPile newPile,
        CardPilePosition position = CardPilePosition.Bottom)
    {
        return AddToPile([card], newPile, position)[0];
    }

    public IReadOnlyList<SimCardPileAddResult> AddToPile(
        IReadOnlyList<PredictedCard> cards,
        PileType newPileType,
        CardPilePosition position = CardPilePosition.Bottom)
    {
        if (cards.Count == 0)
        {
            return [];
        }

        var newPile = State.GetPlayerCombatState(cards[0].Preview.Owner).GetCardPile(newPileType)
            ?? throw new InvalidOperationException(
                $"Cannot find combat pile {newPileType} for player {cards[0].Preview.Owner}.");
        return AddToPile(cards, newPile, position);
    }

    // Mirrors the combat-pile branch of CardPileCmd.Add(IEnumerable<CardModel>, CardPile, ...).
    public IReadOnlyList<SimCardPileAddResult> AddToPile(
        IReadOnlyList<PredictedCard> cards,
        SimCardPile newPile,
        CardPilePosition position = CardPilePosition.Bottom)
    {
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

            var targetPile = newPile;
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

    // Mirrors CardPileCmd.RemoveFromCombat without mutating the real combat piles.
    public void RemoveFromCombat(PredictedCard card)
    {
        RemoveFromCombat([card]);
    }

    // Mirrors CardPileCmd.RemoveFromCombat without mutating the real combat piles.
    public void RemoveFromCombat(IReadOnlyList<PredictedCard> cards)
    {
        if (cards.Count == 0)
        {
            return;
        }

        List<(PredictedCard card, PileType oldPileType)> removedCards = [];

        foreach (var card in cards)
        {
            var pile = card.GetPile(State)
                ?? throw new InvalidOperationException(
                    $"Cannot remove card {card} from combat because it is not in a pile.");
            pile?.Remove(card);
            removedCards.Add((card, pile?.Type ?? PileType.None));
        }

        foreach (var (card, oldPileType) in removedCards)
        {
            // Vanilla dispatches Hook.AfterCardChangedPiles here, which is not mirrored for the same reasons
            // as in AddToPile.
            card.MutablePreview.HasBeenRemovedFromState = true;
        }
    }
}

internal readonly record struct SimCardPileAddResult(
    bool Success,
    PredictedCard CardAdded,
    PileType OldPileType);
