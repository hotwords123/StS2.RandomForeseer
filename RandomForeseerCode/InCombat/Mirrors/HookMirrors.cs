using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Hooks.TurnEnd;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors;

// Simulation-facing facade for mirrored combat hooks, analogous to vanilla Hook. Callers pass
// ordinary hook arguments; this class owns mirror context construction, listener enumeration, and
// hook-level ordering while method-specific registries and contexts remain implementation details.
internal static class HookMirrors
{
    // Mirrors Hook.AfterAutoPostPlayPhaseEntered.
    public static void AfterAutoPostPlayPhaseEntered(CombatPredictionSimulator simulator, Player player)
    {
        var context = new AfterAutoPostPlayMirrorContext { Simulator = simulator, Player = player };

        foreach (var listener in context.State.IterateHookListeners())
        {
            AfterAutoPostPlayPhaseEnteredMirrors.Invoke(listener, context);
        }
    }

    // Mirrors Hook.BeforeSideTurnEnd.
    public static void BeforeSideTurnEnd(
        CombatPredictionSimulator simulator,
        CombatSide side,
        IReadOnlyList<Creature> participants)
    {
        var context = new BeforeSideTurnEndMirrorContext
        {
            Simulator = simulator,
            Side = side,
            Participants = participants
        };

        foreach (var listener in context.State.IterateHookListeners())
        {
            BeforeSideTurnEndMirrors.InvokeVeryEarly(listener, context);
        }

        foreach (var listener in context.State.IterateHookListeners())
        {
            BeforeSideTurnEndMirrors.InvokeEarly(listener, context);
        }

        foreach (var listener in context.State.IterateHookListeners())
        {
            BeforeSideTurnEndMirrors.Invoke(listener, context);
        }
    }
}
