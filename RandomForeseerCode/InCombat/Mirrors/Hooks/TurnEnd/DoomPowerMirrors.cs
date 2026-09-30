using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models.Powers;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Hooks.TurnEnd;

internal static class DoomPowerMirrors
{
    public static void BeforeSideTurnEnd(DoomPower power, SideTurnEndMirrorContext context)
    {
        if (context.Side != CombatSide.Player && ShouldDoomTrigger(power, context))
        {
            KillDoomedCreatures(context.Simulator, context.Side);
        }
    }

    public static void AfterSideTurnEnd(DoomPower power, SideTurnEndMirrorContext context)
    {
        if (context.Side != CombatSide.Enemy && ShouldDoomTrigger(power, context))
        {
            KillDoomedCreatures(context.Simulator, context.Side);
        }
    }

    /// <summary>
    /// Mirrors <see cref="DoomPower.ShouldDoomTrigger"/>.
    /// </summary>
    private static bool ShouldDoomTrigger(DoomPower power, SideTurnEndMirrorContext context)
    {
        return !context.Simulator.IsOverOrEnding &&
               context.Participants.Contains(power.Owner) &&
               context.State.GetCreature(power.Owner).IsAlive &&
               IsOwnerDoomed(context.State, power) &&
               GetDoomedCreatures(context.State, context.Side).First() == power.Owner;
    }

    private static void KillDoomedCreatures(CombatPredictionSimulator simulator, CombatSide side)
    {
        var creatures = GetDoomedCreatures(simulator.State, side).ToArray();
        if (creatures.Length == 0)
        {
            return;
        }

        // AfterDiedToDoom and the remaining death lifecycle are not fully mirrored.
        simulator.History.RecordRisk(PredictionRiskReason.MethodMirrorIncomplete);

        foreach (var creature in creatures)
        {
            simulator.Kill(creature);
        }

        // TODO: Hook.AfterDiedToDoom
    }

    private static IEnumerable<Creature> GetDoomedCreatures(CombatPredictionState state, CombatSide side)
    {
        return state.GetCreaturesOnSide(side)
            .Where(creature => creature.GetPower<DoomPower>() is { } power && IsOwnerDoomed(state, power));
    }

    private static bool IsOwnerDoomed(CombatPredictionState state, DoomPower power)
    {
        return state.GetCreature(power.Owner).CurrentHp <= power.Amount;
    }
}
