using MegaCrit.Sts2.Core.Entities.Players;
using RandomForeseer.RandomForeseerCode.InCombat.Mirrors;

namespace RandomForeseer.RandomForeseerCode.InCombat.Simulation;

internal sealed partial class CombatPredictionSimulator
{
    // Mirrors PlayerCmd.GainEnergy.
    public void GainEnergy(Player player, decimal amount)
    {
        if (IsEnding || amount <= 0m)
        {
            return;
        }

        var modifiedAmount = HookMirrors.ModifyEnergyGain(this, player, amount, out var modifiers);
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
        if (IsEnding || amount <= 0m)
        {
            return;
        }

        State.GetPlayerCombatState(player).LoseEnergy(amount);
    }

    // Mirrors PlayerCmd.GainStars's ending guard, read-only predicate, and state mutation.
    // AfterStarsGained remains outside the current hook-mirror coverage.
    public void GainStars(Player player, decimal amount)
    {
        if (IsEnding || !HookMirrors.ShouldGainStars(this, amount, player))
        {
            return;
        }

        State.GetPlayerCombatState(player).GainStars(amount);
    }
}
