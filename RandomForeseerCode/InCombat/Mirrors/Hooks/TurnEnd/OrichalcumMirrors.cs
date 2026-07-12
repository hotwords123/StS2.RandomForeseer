using MegaCrit.Sts2.Core.Models;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Hooks.TurnEnd;

internal static class OrichalcumMirrors
{
    public static void BeforeSideTurnEndVeryEarly(RelicModel relic, BeforeSideTurnEndMirrorContext context)
    {
        if (context.Participants.Contains(relic.Owner.Creature) &&
            context.State.GetCreature(relic.Owner.Creature).Block <= 0)
        {
            GetState(relic, context).ShouldTrigger = true;
        }
    }

    public static void BeforeSideTurnEnd(RelicModel relic, BeforeSideTurnEndMirrorContext context)
    {
        var state = GetState(relic, context);
        if (state.ShouldTrigger)
        {
            state.ShouldTrigger = false;
            context.Simulator.GainBlock(relic.Owner.Creature, relic.DynamicVars.Block);
        }
    }

    private static State GetState(RelicModel relic, BeforeSideTurnEndMirrorContext context)
    {
        return context.StateStore.Get<State>(relic);
    }

    private sealed class State
    {
        public bool ShouldTrigger { get; set; }
    }
}
