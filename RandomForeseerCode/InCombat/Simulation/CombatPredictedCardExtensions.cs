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
}
