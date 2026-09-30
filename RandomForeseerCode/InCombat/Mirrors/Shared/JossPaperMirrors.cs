using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Hooks.Card;
using RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Hooks.TurnEnd;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Shared;

internal static class JossPaperMirrors
{
    public static void AfterCardExhausted(JossPaper relic, AfterCardExhaustedMirrorContext context)
    {
        if (context.PreviewCard.Owner != relic.Owner)
        {
            return;
        }

        var state = GetState(relic, context);
        if (context.CausedByEthereal)
        {
            state.EtherealCount++;
            return;
        }

        state.CardsExhausted++;
        DrawIfThresholdMet(relic, state, context);
    }

    public static void AfterSideTurnEnd(JossPaper relic, SideTurnEndMirrorContext context)
    {
        if (!context.Participants.Contains(relic.Owner.Creature))
        {
            return;
        }

        var state = GetState(relic, context);
        state.CardsExhausted += state.EtherealCount;
        state.EtherealCount = 0;
        DrawIfThresholdMet(relic, state, context);
    }

    private static State GetState(JossPaper relic, CombatMirrorContext<AbstractModel> context) =>
        context.StateStore.Get(relic, () => new State(relic));

    private static void DrawIfThresholdMet(JossPaper relic, State state, CombatMirrorContext<AbstractModel> context)
    {
        if (state.CardsExhausted < relic.DynamicVars["ExhaustAmount"].BaseValue)
        {
            return;
        }

        var count = (int)(state.CardsExhausted / relic.DynamicVars["ExhaustAmount"].BaseValue);
        context.Simulator.Draw(relic.Owner, count);
        state.CardsExhausted %= relic.DynamicVars["ExhaustAmount"].IntValue;
    }

    private sealed class State(JossPaper relic)
    {
        public int CardsExhausted { get; set; } = relic.CardsExhausted;

        public int EtherealCount { get; set; } = relic.EtherealCount;
    }
}
