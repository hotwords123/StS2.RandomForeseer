using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.InCombat.Mirrors;
using RandomForeseer.RandomForeseerCode.InCombat.Mirrors.CardOnPlay;

namespace RandomForeseer.RandomForeseerCode.InCombat.Simulation;

internal delegate void OnPlayDelegate(PredictedCard card, CardPlay cardPlay);

internal sealed partial class CombatPredictionSimulator
{
    private delegate (PileType PileType, CardPilePosition Position)
        GetResultPileTypeAndPositionForCardPlayDelegate(CardModel card);

    private static readonly GetResultPileTypeAndPositionForCardPlayDelegate
        GetResultPileTypeAndPositionForCardPlay =
            AccessTools.Method(typeof(CardModel), "GetResultPileTypeAndPositionForCardPlay")
                .CreateDelegate<GetResultPileTypeAndPositionForCardPlayDelegate>();

    // Mirrors CardPileCmd.AddDuringManualCardPlay, which is called when a card is manually played
    // from hand and is added to the play pile.
    public void AddDuringManualCardPlay(PredictedCard card)
    {
        card.GetPile(State)?.Remove(card);
        State.GetPlayerCombatState(card.Preview.Owner).PlayPile.Add(card);

        // Vanilla dispatches Hook.AfterCardChangedPiles after visuals finish. This is intentionally
        // skipped currently, for the same reasons as in AddToPile.
    }

    // Mirrors CardCmd.MoveToResultPileWithoutPlaying, not CardModel.MoveToResultPileWithoutPlaying.
    // CardCmd first moves the card to the play pile, then calls the CardModel method; this
    // inlines both steps.
    public void MoveToResultPileWithoutPlaying(PredictedCard card)
    {
        AddToPile(card, PileType.Play);

        if (card.Preview.IsDupe)
        {
            RemoveFromCombat(card);
        }
        else if (card.Preview.ExhaustOnNextPlay || card.GetKeywords(State).Contains(CardKeyword.Exhaust))
        {
            Exhaust(card);
        }
        else
        {
            AddToPile(card, PileType.Discard);
        }
    }

    // Mirrors CardCmd.Discard(PlayerChoiceContext, CardModel).
    // Useful when discarding a single card and drawing no cards.
    public void Discard(PredictedCard card, bool triggerSly = true)
    {
        DiscardAndDraw([card], 0, triggerSly);
    }

    // Mirrors CardCmd.Discard(PlayerChoiceContext, IEnumerable<CardModel>).
    // Useful when discarding multiple cards and drawing no cards.
    public void Discard(IReadOnlyList<PredictedCard> cards, bool triggerSly = true)
    {
        DiscardAndDraw(cards, 0, triggerSly);
    }

    // Mirrors CardCmd.DiscardAndDraw.
    public void DiscardAndDraw(IReadOnlyList<PredictedCard> cardsToDiscard, int cardsToDraw, bool triggerSly = true)
    {
        if (cardsToDiscard.Count == 0 && cardsToDraw == 0)
        {
            return;
        }

        List<PredictedCard> slyCards = [];

        foreach (var card in cardsToDiscard)
        {
            if (triggerSly && card.Preview.IsSlyThisTurn)
            {
                slyCards.Add(card);
            }

            AddToPile(card, PileType.Discard);
            // Vanilla records CardDiscardedHistory here. There are currently no simulated consumers of this history,
            // so it is skipped for now.
            HookMirrors.AfterCardDiscarded(this, card);
        }

        if (cardsToDraw > 0)
        {
            Draw(cardsToDiscard[0].Preview.Owner, cardsToDraw);
        }

        foreach (var slyCard in slyCards)
        {
            AutoPlay(slyCard, type: AutoPlayType.SlyDiscard);
        }
    }

    // Mirrors CardCmd.Exhaust.
    public void Exhaust(PredictedCard card, bool causedByEthereal = false)
    {
        AddToPile(card, PileType.Exhaust);
        // Vanilla records CardExhaustedHistory here. There are currently no simulated consumers of this history,
        // so it is skipped for now.
        HookMirrors.AfterCardExhausted(this, card, causedByEthereal);
    }

    // Mirrors PlayCardAction.ExecuteAction. This is the main entry point for simulating a card play.
    // Note: Resources, ShouldPlay hooks and IsPlayable checks are not simulated here.
    public void ManualPlay(PredictedCard card, Creature? target, OnPlayDelegate? onPlay = null)
    {
        if (card.GetKeywords(State).Contains(CardKeyword.Unplayable) ||
            !card.Preview.IsValidTarget(target))
        {
            return;
        }

        var resources = SpendResources(card, isAutoPlay: false);
        OnPlayWrapper(card, target, isAutoPlay: false, resources, onPlay);
    }

    // Mirrors CardModel.SpendResources, but returns ResourceInfo instead of (int, int) for convenience.
    // Also implements the auto-play logic for capturing X values and star costs, which is handled in CardCmd.AutoPlay
    // in vanilla.
    private ResourceInfo SpendResources(PredictedCard card, bool isAutoPlay, bool skipXCapture = false)
    {
        var playerCombatState = State.GetPlayerCombatState(card.Preview.Owner);
        var energyValue = card.GetEnergyCostWithModifiers(State, playerCombatState);
        var starValue = card.GetStarCostWithModifiers(State, playerCombatState);

        if (!isAutoPlay)
        {
            // Vanilla checks Hook.ShouldPayExcessEnergyCostWithStars here, but there are no known consumers
            // of this hook, so it is skipped for now.
        }

        if (!skipXCapture)
        {
            if (card.Preview.EnergyCost.CostsX)
            {
                card.MutablePreview.EnergyCost.CapturedXValue = energyValue;
            }
            card.MutablePreview.LastStarsSpent = starValue;
        }

        if (isAutoPlay)
        {
            return new ResourceInfo
            {
                EnergySpent = 0,
                EnergyValue = energyValue,
                StarsSpent = 0,
                StarValue = starValue
            };
        }

        // Mirrors CardModel.SpendEnergy and CardModel.SpendStars.
        if (energyValue > 0)
        {
            // TODO: Record EnergySpent history.
            playerCombatState.LoseEnergy(energyValue);
        }
        // TODO: Dispatch Hook.AfterEnergySpent.

        card.MutablePreview.LastStarsSpent = starValue;
        if (starValue > 0)
        {
            playerCombatState.LoseStars(starValue);
            // TODO: Record StarsSpent history.
            // TODO: Dispatch Hook.AfterStarsSpent.
        }

		return new ResourceInfo
        {
            EnergySpent = energyValue,
            EnergyValue = energyValue,
            StarsSpent = starValue,
            StarValue = starValue
        };
    }

    // Mirrors CardModel.OnPlayWrapper. ResourceInfo is not simulated yet.
    private void OnPlayWrapper(
        PredictedCard card,
        Creature? target,
        bool isAutoPlay,
        ResourceInfo resources,
        OnPlayDelegate? onPlay)
    {
        using var _ = PushSource(card.Original);

        var previewCard = card.MutablePreview;
        previewCard.CurrentTarget = target;
        previewCard.CurrentPlayIndex = 0;

        if (isAutoPlay)
        {
            AddToPile(card, PileType.Play);
        }
        else
        {
            AddDuringManualCardPlay(card);
        }

        var (resultPileType, resultPilePosition) = GetResultPileTypeAndPositionForCardPlay(previewCard);
        (resultPileType, resultPilePosition) = Hook.ModifyCardPlayResultPileTypeAndPosition(
            State.CombatState,
            previewCard,
            isAutoPlay,
            resources,
            resultPileType,
            resultPilePosition,
            out var modifiers);
        foreach (var modifier in modifiers)
        {
            // TODO: Dispatch Hook.AfterCardPlayResultPileTypeAndPositionModified.
            using (PushSource(modifier))
            {
                MarkCurrentSourceRisky();
            }
        }

        var playCount = card.GeneratePlayCount(this, target);
        var ownerCreature = State.GetCreature(previewCard.Owner.Creature);
        if (ownerCreature.IsDead)
        {
            return;
        }

        for (var i = 0; i < playCount; i++)
        {
            previewCard.CurrentPlayIndex = i;

            var cardPlay = new CardPlay
            {
                Card = previewCard,
                Target = target,
                ResultPile = resultPileType,
                Resources = resources,
                IsAutoPlay = isAutoPlay,
                PlayIndex = i,
                PlayCount = playCount
            };

            // TODO: Dispatch BeforeCardPlayed hooks
            // TODO: Record CardPlayStarted history

            if (onPlay is null)
            {
                CardOnPlayMirrors.Invoke(this, card, cardPlay);
            }
            else
            {
                onPlay(card, cardPlay);
            }

            if (ownerCreature.IsDead)
            {
                return;
            }

            if (previewCard.Enchantment is { } enchantment)
            {
                // TODO: Simulate enchantment effects
            }

            if (previewCard.Affliction is { } affliction)
            {
                // TODO: Simulate affliction effects
            }

            // TODO: Record CardPlayFinished history
            // TODO: Dispatch AfterCardPlayed hooks

            if (ownerCreature.IsDead)
            {
                return;
            }
        }

        if (card.GetPile(State)?.Type is PileType.Play)
        {
            switch (resultPileType)
            {
                case PileType.None:
                    RemoveFromCombat(card);
                    break;
                case PileType.Exhaust:
                    Exhaust(card);
                    break;
                default:
                    AddToPile(card, resultPileType, resultPilePosition);
                    break;
            }
        }

        // TODO: Check for empty hand

        previewCard.EnergyCost.AfterCardPlayedCleanup();
        previewCard._temporaryStarCosts.RemoveAll(cost => cost.ClearsWhenCardIsPlayed);

        previewCard.CurrentTarget = null;
        previewCard.CurrentPlayIndex = 0;
    }

    // Mirrors CardModel.Afflict<T>.
    public T? Afflict<T>(PredictedCard card, decimal amount) where T : AfflictionModel
    {
        return Afflict(ModelDb.Affliction<T>().ToMutable(), card, amount) as T;
    }

    // Mirrors CardModel.Afflict.
    public AfflictionModel? Afflict(AfflictionModel affliction, PredictedCard card, decimal amount)
    {
        affliction.AssertMutable();

        if (!Hook.ShouldAfflict(State.CombatState, card.Preview, affliction) ||
            !affliction.CanAfflict(card.Preview))
        {
            return null;
        }

        if (card.Preview.Affliction == null)
        {
            card.Afflict(affliction, amount);
            // Currently, no vanilla affliction overrides AfterApplied, but it is called here for completeness.
            affliction.AfterApplied();
        }
        else
        {
            if (card.Preview.Affliction.GetType() != affliction.GetType())
            {
                return null;
            }

            // We don't use AfflictionModel.Amount here because its setter recalculates values through
            // the real owner PlayerCombatState even though this is only a preview card.
            card.MutablePreview.Affliction!._amount += (int)amount;
        }

        History.CardAfflicted(card, affliction);
        return card.Preview.Affliction;
    }
}
