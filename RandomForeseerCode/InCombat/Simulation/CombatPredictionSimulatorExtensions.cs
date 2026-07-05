using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace RandomForeseer.RandomForeseerCode.InCombat.Simulation;

internal static class CombatPredictionSimulatorExtensions
{
    public static IReadOnlyList<IReadOnlyList<DamageResult>> Simulate(
        this AttackCommand attackCommand,
        CombatPredictionSimulator simulator)
    {
        return simulator.ExecuteAttack(attackCommand);
    }
}
