using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using RandomForeseer.RandomForeseerCode.Common;

namespace RandomForeseer.RandomForeseerCode.InCombat.Simulation;

internal static class CombatPredictedCardExtensions
{
    // Mirrors CardModel.Pile property, but returns the simulated pile instead of the actual pile.
    public static SimCardPile? GetPile(this PredictedCard card, CombatPredictionState state)
    {
        return card.Preview.Owner is { } owner
            ? GetPile(card, state.GetPlayerCombatState(owner))
            : null;
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
        var playCount = Hook.ModifyCardPlayCount(
            simulator.State.CombatState,
            card.Preview,
            card.Preview.GetEnchantedReplayCount() + 1,
            target,
            out var modifiers);
        if (modifiers.Count > 0)
        {
            // Vanilla GeneratePlayCount would also run AfterModifyingCardPlayCount here.
            // Those listeners can decrement/remove live powers or relic state, so prediction
            // uses the value hook for energy amount and marks the missing state commit as risk.
            simulator.History.RecordRisk(PredictionRiskReason.MethodMirrorIncomplete);
        }
        return playCount;
    }

    // Mirrors CardEnergyCost.GetAmountToSpend.
    public static int GetEnergyCostWithModifiers(
        this PredictedCard card,
        CombatPredictionState state,
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

        // TODO: Simulate these hooks to read from the predicted state instead of the real state.
        // Direct Hook calls here can drift from vanilla after simulated pile/state changes,
        // because cost hooks may read live CardModel.Pile, combat history, or model-local counters.
        cost = (int)Hook.ModifyEnergyCostInCombat(state.CombatState, card.Preview, cost);
        return Math.Max(0, cost);
    }

    // Mirrors CardModel.GetStarCostWithModifiers.
    public static int GetStarCostWithModifiers(
        this PredictedCard card,
        CombatPredictionState state,
        SimPlayerCombatState playerCombatState)
    {
        if (card.Preview.HasStarCostX)
        {
            return playerCombatState.Stars;
        }

        var cost = card.Preview.CurrentStarCost;
        // TODO: Simulate these hooks to read from the predicted state instead of the real state.
        cost = (int)Hook.ModifyStarCost(state.CombatState, card.Preview, cost);
        return Math.Max(0, cost);
    }

    // Mirrors CardModel.ResolveEnergyXValue.
    public static int ResolveEnergyXValue(this PredictedCard card, CombatPredictionState state)
    {
        return Hook.ModifyXValue(state.CombatState, card.Preview, card.Preview.EnergyCost.CapturedXValue);
    }

    // Mirrors CardModel.ResolveStarXValue.
    public static int ResolveStarXValue(this PredictedCard card, CombatPredictionState state)
    {
        return Hook.ModifyXValue(state.CombatState, card.Preview, card.Preview.LastStarsSpent);
    }

    // Mirrors CardModel.Keywords => CardModel.GetKeywordsWithSources(KeywordSources.All).
    public static IReadOnlySet<CardKeyword> GetKeywords(this PredictedCard card, CombatPredictionState state)
    {
        var keywords = card.Preview.LocalKeywords.ToHashSet();
        Hook.ModifyKeywordsInCombat(state.CombatState, card.Preview, keywords);
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
