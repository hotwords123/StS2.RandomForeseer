using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Creatures;
using RandomForeseer.RandomForeseerCode.Common;

namespace RandomForeseer.RandomForeseerCode.InCombat.Simulation;

internal static class CombatPredictionSimulatorExtensions
{
    // Convenience extension method to simulate an attack command.
    public static IReadOnlyList<IReadOnlyList<DamageResult>> Simulate(
        this AttackCommand attackCommand,
        CombatPredictionSimulator simulator)
    {
        return simulator.ExecuteAttack(attackCommand);
    }

    // Convenience extension method to simulate a single-targeted attack command.
    public static bool TrySimulateTargetedAttack(
        this CombatPredictionSimulator simulator,
        PredictedCard source,
        Creature? target,
        int hitCount = 1)
    {
        if (target is null || !source.Preview.CanPlayTargeting(target))
        {
            return false;
        }

        DamageCmd.Attack(source.Preview.DynamicVars.Damage.BaseValue)
            .FromCard(source.Preview, null)
            .WithHitCount(hitCount)
            .Targeting(target)
            .Simulate(simulator);

        return true;
    }
}
