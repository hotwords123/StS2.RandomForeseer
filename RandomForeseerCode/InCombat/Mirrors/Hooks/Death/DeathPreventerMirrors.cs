using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Models.Relics;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Hooks.Death;

internal static class FairyInABottleMirrors
{
    public static bool ShouldDie(FairyInABottle potion, ShouldDieMirrorContext context)
    {
        if (context.Creature == potion.Owner.Creature)
        {
            return GetState(potion, context).WasUsed;
        }

        return true;
    }

    public static void AfterPreventingDeath(
        FairyInABottle potion,
        AfterPreventingDeathMirrorContext context)
    {
        GetState(potion, context).WasUsed = true;

        // Mirrors FairyInABottle.OnUse's heal only. Vanilla reaches it through OnUseWrapper,
        // whose BeforePotionUsed/AfterPotionUsed hooks are not mirrored here.
        context.Simulator.Heal(context.Creature, Math.Max(1m, context.Creature.MaxHp * 0.3m));
    }

    private static State GetState(FairyInABottle potion, CombatPredictionMirrorContext context)
    {
        return context.StateStore.Get<State>(potion);
    }

    private sealed class State
    {
        public bool WasUsed { get; set; }
    }
}

internal static class LizardTailMirrors
{
    public static bool ShouldDieLate(LizardTail relic, ShouldDieMirrorContext context)
    {
        if (context.Creature == relic.Owner.Creature)
        {
            return GetState(relic, context).WasUsed;
        }

        return true;
    }

    public static void AfterPreventingDeath(LizardTail relic, AfterPreventingDeathMirrorContext context)
    {
        GetState(relic, context).WasUsed = true;

        var amount = Math.Max(1m, context.Creature.MaxHp * (relic.DynamicVars.Heal.BaseValue / 100m));
        context.Simulator.Heal(context.Creature, amount);
    }

    private static State GetState(LizardTail relic, CombatPredictionMirrorContext context)
    {
        return context.StateStore.Get(relic, () => new State(relic));
    }

    private sealed class State(LizardTail relic)
    {
        public bool WasUsed { get; set; } = relic.WasUsed;
    }
}
