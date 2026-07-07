using System.Diagnostics.CodeAnalysis;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Models.Relics;
using RandomForeseer.RandomForeseerCode.Common.Hooks;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;

namespace RandomForeseer.RandomForeseerCode.InCombat.Hooks;

internal static class DeathPreventHooks
{
    private static readonly HookSpec ShouldDie = new(
        nameof(AbstractModel.ShouldDie),
        [typeof(Creature)]);

    private static readonly HookSpec ShouldDieLate = new(
        nameof(AbstractModel.ShouldDieLate),
        [typeof(Creature)]);

    private static readonly HookSpec AfterPreventingDeath = new(
        nameof(AbstractModel.AfterPreventingDeath),
        [typeof(Creature)]);

    private static readonly HookRegistry<ShouldDieHookContext> ShouldDieRegistry = CreateShouldDieRegistry();

    private static readonly HookRegistry<ShouldDieHookContext> ShouldDieLateRegistry = CreateShouldDieLateRegistry();

    private static readonly HookRegistry<AfterPreventingDeathHookContext> AfterPreventingDeathRegistry =
        CreateAfterPreventingDeathRegistry();

    public static bool RunShouldDie(
        CombatPredictionSimulator simulator,
        Creature creature,
        [NotNullWhen(false)] out AbstractModel? preventer)
    {
        var context = new ShouldDieHookContext
        {
            Simulator = simulator,
            Creature = creature
        };

        ShouldDieRegistry.Run(context.RunState.IterateHookListeners(context.CombatState), context);
        ShouldDieLateRegistry.Run(context.RunState.IterateHookListeners(context.CombatState), context);

        preventer = context.Preventer;
        return preventer is null;
    }

    public static void RunAfterPreventingDeath(CombatPredictionSimulator simulator, AbstractModel preventer, Creature creature)
    {
        var context = new AfterPreventingDeathHookContext
        {
            Simulator = simulator,
            Creature = creature
        };

        if (context.RunState.IterateHookListeners(context.CombatState).Contains(preventer))
        {
            AfterPreventingDeathRegistry.Run([preventer], context);
        }
    }

    private static HookRegistry<ShouldDieHookContext> CreateShouldDieRegistry()
    {
        var registry = new HookRegistry<ShouldDieHookContext>(ShouldDie);

        registry.Register<FairyInABottle>(HandleFairyInABottle);

        return registry;
    }

    private static HookRegistry<ShouldDieHookContext> CreateShouldDieLateRegistry()
    {
        var registry = new HookRegistry<ShouldDieHookContext>(ShouldDieLate);

        registry.Register<LizardTail>(HandleLizardTail);

        return registry;
    }

    private static HookRegistry<AfterPreventingDeathHookContext> CreateAfterPreventingDeathRegistry()
    {
        var registry = new HookRegistry<AfterPreventingDeathHookContext>(AfterPreventingDeath);

        registry.Register<FairyInABottle>(HandleFairyInABottleAfterPreventing);
        registry.Register<LizardTail>(HandleLizardTailAfterPreventing);

        return registry;
    }

    private static void HandleFairyInABottle(FairyInABottle potion, ShouldDieHookContext context)
    {
        if (context.Creature != potion.Owner.Creature)
        {
            return;
        }

        var state = context.StateStore.Get(potion, static () => new PreventDeathPredictionState());
        if (!state.WasUsed)
        {
            context.SetPreventer(potion);
        }
    }

    private static void HandleLizardTail(LizardTail relic, ShouldDieHookContext context)
    {
        if (context.Creature != relic.Owner.Creature)
        {
            return;
        }

        var state = context.StateStore.Get(relic, () => new PreventDeathPredictionState
        {
            WasUsed = relic.WasUsed
        });
        if (!state.WasUsed)
        {
            context.SetPreventer(relic);
        }
    }

    private static void HandleFairyInABottleAfterPreventing(FairyInABottle potion, AfterPreventingDeathHookContext context)
    {
        if (context.Creature != potion.Owner.Creature)
        {
            return;
        }

        var state = context.StateStore.Get(potion, static () => new PreventDeathPredictionState());
        if (state.WasUsed)
        {
            return;
        }

        state.WasUsed = true;

        // Mirrors FairyInABottle.OnUse's heal only. Vanilla reaches it through OnUseWrapper,
        // whose BeforePotionUsed/AfterPotionUsed hooks are not mirrored here.
        context.Simulator.Heal(context.Creature, Math.Max(1m, context.Creature.MaxHp * 0.3m));
    }

    private static void HandleLizardTailAfterPreventing(LizardTail relic, AfterPreventingDeathHookContext context)
    {
        if (context.Creature != relic.Owner.Creature)
        {
            return;
        }

        var state = context.StateStore.Get(relic, () => new PreventDeathPredictionState
        {
            WasUsed = relic.WasUsed
        });
        if (state.WasUsed)
        {
            return;
        }

        state.WasUsed = true;

        context.Simulator.Heal(
            context.Creature,
            Math.Max(1m, context.Creature.MaxHp * (relic.DynamicVars.Heal.BaseValue / 100m)));
    }
}

internal sealed class PreventDeathPredictionState
{
    public bool WasUsed { get; set; }
}

internal sealed class ShouldDieHookContext : CombatPredictionHookContext
{
    public required Creature Creature { get; init; }

    public AbstractModel? Preventer { get; private set; }

    public override bool ShouldContinue => Preventer is null;

    public void SetPreventer(AbstractModel preventer)
    {
        Preventer = preventer;
    }
}

internal sealed class AfterPreventingDeathHookContext : CombatPredictionHookContext
{
    public required Creature Creature { get; init; }
}
