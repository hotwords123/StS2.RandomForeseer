using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.InCombat.Hooks;

namespace RandomForeseer.RandomForeseerCode.InCombat.Simulation;

internal sealed partial class CombatPredictionSimulator
{
    private delegate (PileType PileType, int Position)
        GetResultPileTypeAndPositionForCardPlayDelegate(CardModel card);

    private static readonly GetResultPileTypeAndPositionForCardPlayDelegate
        GetResultPileTypeAndPositionForCardPlay =
            AccessTools
                .Method(typeof(CardModel), "GetResultPileTypeAndPositionForCardPlay")
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
        else if (card.Preview.ExhaustOnNextPlay || card.Preview.Keywords.Contains(CardKeyword.Exhaust))
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
            AfterCardDiscardedHook.Run(new AfterCardDiscardedHookContext
            {
                Simulator = this,
                Card = card
            });
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
        AfterCardExhaustedHook.Run(new AfterCardExhaustedHookContext
        {
            Simulator = this,
            Card = card,
            CausedByEthereal = causedByEthereal
        });
    }

    // Mirrors CardModel.OnPlayWrapper. ResourceInfo is not simulated yet.
    public void Play(PredictedCard card, Creature? target, bool isAutoPlay, Action onPlay)
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

        // TODO: Determine the result pile and position for the card play

        var playCount = card.GeneratePlayCount(this, target);
        var ownerCreature = State.GetCreature(previewCard.Owner.Creature);
        if (ownerCreature.IsDead)
        {
            return;
        }

        for (var i = 0; i < playCount; i++)
        {
            previewCard.CurrentPlayIndex = i;

            // TODO: Construct CardPlay
            // TODO: Dispatch BeforeCardPlayed hooks
            // TODO: Record CardPlayStarted history

            onPlay();

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

        // TODO: Move card to the appropriate result pile and position after play
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

        RecordCardAfflictedHistory(card, affliction);
        return card.Preview.Affliction;
    }
}
