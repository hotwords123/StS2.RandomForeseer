using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.InCombat.Mirrors;

namespace RandomForeseer.RandomForeseerCode.InCombat.Simulation;

internal static class CombatPredictedCardExtensions
{
    // Mirrors CardModel.Pile property, but returns the simulated pile instead of the actual pile.
    public static SimCardPile? GetPile(this PredictedCard card, CombatPredictionState state)
    {
        return card.GetPile(state.GetPlayerCombatState(card.Preview.Owner));
    }

    // Mirrors CardModel.Pile property, but returns the simulated pile instead of the actual pile.
    public static SimCardPile? GetPile(this PredictedCard card, SimPlayerCombatState playerCombatState)
    {
        return playerCombatState.AllPiles.FirstOrDefault(pile => pile.Cards.Contains(card));
    }

    // Mirrors CardModel.CreateClone, but returns a PredictedCard instead of a CardModel.
    public static PredictedCard CreateClone(this PredictedCard card)
    {
        var clonedCard = (CardModel)card.Preview.MutableClone();
        clonedCard._cloneOf = card.Original;
        clonedCard.ExhaustOnNextPlay = false;
        return PredictedCard.FromGenerated(clonedCard);
    }

    /// <summary>
    /// Mirrors <see cref="CardModel.CreateCloneForPlayer"/>.
    /// </summary>
    public static PredictedCard CreateCloneForPlayer(this PredictedCard card, Player player)
    {
        var clone = card.CreateClone();
        clone.MutablePreview._owner = player;
        return clone;
    }

    // Mirrors CardModel.AfflictInternal without firing live-model side effects. Preview cards still
    // keep the real owner, so AfflictInternal's Amount setter and AfflictionChanged event would
    // recalculate values through the real PlayerCombatState and notify real card listeners.
    public static void Afflict(this PredictedCard card, AfflictionModel affliction, decimal amount)
    {
        var previewCard = card.MutablePreview;
        previewCard.Affliction = affliction;
        previewCard.Affliction.Card = previewCard;
        previewCard.Affliction._amount = (int)amount;
    }

    // Mirrors CardModel.ClearAfflictionInternal without firing live-model side effects.
    public static void ClearAffliction(this PredictedCard card)
    {
        if (card.Preview.Affliction != null)
        {
            var previewCard = card.MutablePreview;
            previewCard.Affliction!.ClearInternal();
            previewCard.Affliction = null;
        }
    }

    // Mirrors CardModel.GeneratePlayCount.
    public static int GeneratePlayCount(this PredictedCard card, CombatPredictionSimulator simulator, Creature? target)
    {
        var playCount = HookMirrors.ModifyCardPlayCount(
            simulator,
            card,
            card.Preview.GetEnchantedReplayCount() + 1,
            target,
            out var modifiers);
        HookMirrors.AfterModifyingCardPlayCount(simulator, card, modifiers);
        return playCount;
    }

    // Mirrors CardEnergyCost.GetAmountToSpend.
    public static int GetEnergyCostWithModifiers(
        this PredictedCard card,
        CombatPredictionSimulator simulator,
        SimPlayerCombatState playerCombatState)
    {
        var energyCost = card.Preview.EnergyCost;
        if (energyCost.CostsX)
        {
            return playerCombatState.Energy;
        }

        var cost = energyCost._base;
        if (cost < 0)
        {
            return 0;
        }

        foreach (var modifier in energyCost._localModifiers)
        {
            cost = modifier.Modify(cost);
        }

        cost = (int)HookMirrors.ModifyEnergyCostInCombat(simulator, card, cost);
        return Math.Max(0, cost);
    }

    // Mirrors CardModel.GetStarCostWithModifiers.
    public static int GetStarCostWithModifiers(
        this PredictedCard card,
        CombatPredictionSimulator simulator,
        SimPlayerCombatState playerCombatState)
    {
        if (card.Preview.HasStarCostX)
        {
            return playerCombatState.Stars;
        }

        var cost = card.Preview.CurrentStarCost;
        cost = (int)HookMirrors.ModifyStarCost(simulator, card, cost);
        return Math.Max(0, cost);
    }

    // Mirrors CardModel.Keywords => CardModel.GetKeywordsWithSources(KeywordSources.All).
    public static IReadOnlySet<CardKeyword> GetKeywords(this PredictedCard card, CombatPredictionSimulator simulator)
    {
        var keywords = card.Preview.LocalKeywords.ToHashSet();
        HookMirrors.ModifyKeywordsInCombat(simulator, card.Preview, keywords);
        return keywords;
    }

    // Forwards to CardModel.SetToFreeThisTurn, but returns the same PredictedCard for fluent chaining.
    public static PredictedCard SetToFreeThisTurn(this PredictedCard card)
    {
        card.MutablePreview.SetToFreeThisTurn();
        return card;
    }

    // Forwards to CardModel.SetToFreeThisCombat, but returns the same PredictedCard for fluent chaining.
    public static PredictedCard SetToFreeThisCombat(this PredictedCard card)
    {
        card.MutablePreview.SetToFreeThisCombat();
        return card;
    }
}
