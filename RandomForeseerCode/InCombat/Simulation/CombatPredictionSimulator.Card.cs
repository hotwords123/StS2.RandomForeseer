using System.Diagnostics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Models;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.InCombat.Mirrors;
using RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Afflictions.OnPlay;
using RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Cards;
using RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Cards.OnPlay;
using RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Enchantments.OnPlay;

namespace RandomForeseer.RandomForeseerCode.InCombat.Simulation;

internal sealed partial class CombatPredictionSimulator
{
    // Mirrors CardPileCmd.AddDuringManualCardPlay, which is called when a card is manually played
    // from hand and is added to the play pile.
    public void AddDuringManualCardPlay(PredictedCard card)
    {
        if (IsOverOrEnding)
        {
            return;
        }

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
        else if (card.Preview.ExhaustOnNextPlay || card.GetKeywords(this).Contains(CardKeyword.Exhaust))
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
    public void Discard(PredictedCard card)
    {
        DiscardAndDraw([card], 0);
    }

    // Mirrors CardCmd.Discard(PlayerChoiceContext, IEnumerable<CardModel>).
    // Useful when discarding multiple cards and drawing no cards.
    public void Discard(IReadOnlyList<PredictedCard> cards)
    {
        DiscardAndDraw(cards, 0);
    }

    // Mirrors CardCmd.DiscardAndDraw.
    public void DiscardAndDraw(IReadOnlyList<PredictedCard> cardsToDiscard, int cardsToDraw)
    {
        if (IsOverOrEnding || cardsToDiscard.Count == 0 && cardsToDraw == 0)
        {
            return;
        }

        List<PredictedCard> slyCards = [];

        foreach (var card in cardsToDiscard)
        {
            if (card.Preview.IsSlyThisTurn)
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
        if (IsOverOrEnding)
        {
            return;
        }

        AddToPile(card, PileType.Exhaust);
        // Vanilla records CardExhaustedHistory here. There are currently no simulated consumers of this history,
        // so it is skipped for now.
        HookMirrors.AfterCardExhausted(this, card, causedByEthereal);
    }

    /// <summary>
    /// Mirrors the prediction-relevant portion of <see cref="PlayCardAction.ExecuteAction"/> for a manual card play.
    /// </summary>
    /// <param name="card">The prediction-owned card wrapper to play.</param>
    /// <param name="target">The already-resolved target, if required.</param>
    /// <param name="frame">The exact root card-play frame.</param>
    /// <remarks>
    /// The returned frame has <see cref="PredictedCard.Original"/> as its source and
    /// <see cref="PredictionActionKind.CardPlay"/> as its action. It remains a stable identity after its trace scope is
    /// disposed and must be paired only with this simulator's history. Card playability and target validation checks
    /// are outside this entry point; callers must perform any required UI/target gating before invocation.
    /// </remarks>
    public void ManualPlay(PredictedCard card, Creature? target, out PredictionTraceFrame frame)
    {
        var resources = SpendResources(card, isAutoPlay: false);
        OnPlayWrapper(card, target, isAutoPlay: false, resources, out frame);
    }

    /// <summary>
    /// Mirrors <see cref="CardModel.CanPlay()"/>.
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="CardModel.CanPlay()"/>, this mirror does not check whether there are living allies
    /// for <see cref="TargetType.AnyAlly"/> cards, since <see cref="CardModel.IsValidTarget(Creature?)"/>
    /// already rejects null or dead ally targets.
    /// </remarks>
    public bool CanPlay(PredictedCard card)
    {
        if (card.GetKeywords(this).Contains(CardKeyword.Unplayable))
        {
            return false;
        }

        var ownerState = State.GetPlayerCombatState(card.Preview.Owner);
        var energyCost = card.GetEnergyCostWithModifiers(this, ownerState);
        var starCost = card.GetStarCostWithModifiers(this, ownerState);

        if (energyCost > ownerState.Energy &&
            HookMirrors.ShouldPayExcessEnergyCostWithStars(this, card.Preview.Owner))
        {
            starCost += 2 * (energyCost - ownerState.Energy);
            energyCost = ownerState.Energy;
        }

        if (energyCost > ownerState.Energy || starCost > ownerState.Stars)
        {
            return false;
        }

        if (!HookMirrors.ShouldPlay(this, card, out _, AutoPlayType.None))
        {
            return false;
        }

        if (!CardIsPlayableMirrors.Invoke(this, card))
        {
            return false;
        }

        return true;
    }

    // Mirrors CardModel.SpendResources, but returns ResourceInfo instead of (int, int) for convenience.
    // Also implements the auto-play logic for capturing X values and star costs, which is handled in CardCmd.AutoPlay
    // in vanilla.
    private ResourceInfo SpendResources(PredictedCard card, bool isAutoPlay, bool skipXCapture = false)
    {
        var playerCombatState = State.GetPlayerCombatState(card.Preview.Owner);
        var energyValue = card.GetEnergyCostWithModifiers(this, playerCombatState);
        var starValue = card.GetStarCostWithModifiers(this, playerCombatState);

        if (!isAutoPlay && energyValue > playerCombatState.Energy &&
            HookMirrors.ShouldPayExcessEnergyCostWithStars(this, card.Preview.Owner))
        {
            starValue += 2 * (energyValue - playerCombatState.Energy);
            energyValue = playerCombatState.Energy;
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
            History.EnergySpent(card.Preview.Owner, energyValue);
            playerCombatState.LoseEnergy(energyValue);
        }
        // TODO: Dispatch Hook.AfterEnergySpent.

        card.MutablePreview.LastStarsSpent = starValue;
        if (starValue > 0)
        {
            var previousStars = playerCombatState.Stars;
            playerCombatState.LoseStars(starValue);
            if (playerCombatState.Stars != previousStars)
            {
                History.StarsModified(card.Preview.Owner, playerCombatState.Stars - previousStars);
            }
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

    /// <summary>
    /// Mirrors <see cref="CardModel.OnPlayWrapper"/>.
    /// </summary>
    private void OnPlayWrapper(
        PredictedCard card,
        Creature? target,
        bool isAutoPlay,
        ResourceInfo resources,
        out PredictionTraceFrame frame)
    {
        using var _ = PushActionSource(card.Original, PredictionActionKind.CardPlay);
        frame = CurrentFrame ?? throw new UnreachableException("No current frame after pushing action source.");

        var previewCard = card.MutablePreview;
        var originalOwner = previewCard.Owner;
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

        var resultLocation = CardResultLocationMirrors.GetResultLocation(this, card);
        resultLocation = HookMirrors.ModifyCardPlayResultLocation(
            this,
            card,
            isAutoPlay,
            resources,
            resultLocation,
            out var resultLocationModifiers);
        HookMirrors.AfterModifyingCardPlayResultLocation(
            this,
            card,
            resultLocation,
            resultLocationModifiers);

        var playCount = card.GeneratePlayCount(this, target);
        var ownerCreature = State.GetCreature(originalOwner.Creature);
        if (ownerCreature.IsDead)
        {
            return;
        }

        for (var i = 0; i < playCount; i++)
        {
            if (IsOverOrEnding)
            {
                break;
            }

            previewCard.CurrentPlayIndex = i;

            var cardPlay = new CardPlay
            {
                Card = previewCard,
                Player = originalOwner,
                Target = target,
                ResultPile = resultLocation.pileType,
                Resources = resources,
                IsAutoPlay = isAutoPlay,
                PlayIndex = i,
                PlayCount = playCount
            };

            HookMirrors.BeforeCardPlayed(this, card, cardPlay);
            History.CardPlayStarted(card, cardPlay);

            CardOnPlayMirrors.Invoke(this, card, cardPlay);

            if (ownerCreature.IsDead)
            {
                return;
            }

            if (previewCard.Enchantment is { } enchantment)
            {
                EnchantmentOnPlayMirrors.Invoke(this, card, cardPlay, enchantment);

                if (ownerCreature.IsDead)
                {
                    return;
                }
            }

            if (previewCard.Affliction is { } affliction)
            {
                AfflictionOnPlayMirrors.Invoke(this, card, target, affliction);

                if (ownerCreature.IsDead)
                {
                    return;
                }
            }

            History.CardPlayFinished(
                card,
                cardPlay,
                card.GetKeywords(this).Contains(CardKeyword.Ethereal));
            HookMirrors.AfterCardPlayed(this, card, cardPlay);

            if (ownerCreature.IsDead)
            {
                return;
            }
        }

        if (originalOwner != resultLocation.player && resultLocation.pileType != PileType.None)
        {
            GiveToAnotherPlayer(
                card,
                originalOwner,
                resultLocation.player,
                resultLocation.pileType,
                resultLocation.position);
        }

        if (card.GetPile(State)?.Type is PileType.Play)
        {
            switch (resultLocation.pileType)
            {
                case PileType.None:
                    RemoveFromCombat(card);
                    break;
                case PileType.Exhaust:
                    Exhaust(card);
                    break;
                case PileType.Draw or PileType.Hand or PileType.Discard:
                    AddToPile(card, resultLocation.pileType, resultLocation.position);
                    break;
                default:
                    Entry.Logger.Warn($"Unexpected result pile type {resultLocation.pileType} for {previewCard.Id}.");
                    RemoveFromCombat(card);
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

    /// <summary>
    /// Mirrors <see cref="CardCmd.Afflict(AfflictionModel, CardModel, decimal)"/>.
    /// </summary>
    public AfflictionModel? Afflict(AfflictionModel affliction, PredictedCard card, decimal amount)
    {
        if (IsOverOrEnding)
        {
            return null;
        }

        affliction.AssertMutable();

        if (!HookMirrors.ShouldAfflict(this, card.Preview, affliction) ||
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

    /// <summary>
    /// Mirrors <see cref="CardCmd.Upgrade(CardModel, MegaCrit.Sts2.Core.Nodes.CommonUi.CardPreviewStyle)"/>.
    /// </summary>
    public bool Upgrade(PredictedCard card)
    {
        if (IsEnding)
        {
            return false;
        }

        card.Upgrade();
        return true;
    }
}
