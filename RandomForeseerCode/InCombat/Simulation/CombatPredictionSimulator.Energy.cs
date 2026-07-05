using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;

namespace RandomForeseer.RandomForeseerCode.InCombat.Simulation;

internal sealed partial class CombatPredictionSimulator
{
    // Mirrors PlayerCmd.GainEnergy.
    public void GainEnergy(Player player, decimal amount)
    {
        if (amount <= 0m)
        {
            return;
        }

        var modifiedAmount = Hook.ModifyEnergyGain(combatState, player, amount, out var modifiers);
        // Mirrors PlayerCmd.GainEnergy's value hook. AfterModifyingEnergyGain is
        // intentionally not mirrored: reviewed vanilla listeners only flash UI and
        // do not mutate prediction-relevant state.
        _ = modifiers;

        if (modifiedAmount > 0m)
        {
            State.GetPlayerCombatState(player).GainEnergy(modifiedAmount);
        }
    }

    // Mirrors PlayerCmd.LoseEnergy.
    public void LoseEnergy(Player player, decimal amount)
    {
        if (amount <= 0m)
        {
            return;
        }

        State.GetPlayerCombatState(player).LoseEnergy(amount);
    }
}
