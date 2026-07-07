using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Orbs;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.ValueProps;
using RandomForeseer.RandomForeseerCode.Common.Hooks;

namespace RandomForeseer.RandomForeseerCode.InCombat.Hooks;

internal static class OrbHooks
{
    private static readonly HookSpec AfterOrbChanneled = new(
        nameof(AbstractModel.AfterOrbChanneled),
        [
            typeof(PlayerChoiceContext),
            typeof(Player),
            typeof(OrbModel)
        ]);

    private static readonly HookSpec AfterOrbEvoked = new(
        nameof(AbstractModel.AfterOrbEvoked),
        [
            typeof(PlayerChoiceContext),
            typeof(OrbModel),
            typeof(IEnumerable<Creature>)
        ]);

    private static readonly HookRegistry<AfterOrbChanneledHookContext> AfterOrbChanneledRegistry =
        CreateAfterOrbChanneledRegistry();

    private static readonly HookRegistry<AfterOrbEvokedHookContext> AfterOrbEvokedRegistry =
        CreateAfterOrbEvokedRegistry();

    public static void RunAfterOrbChanneled(AfterOrbChanneledHookContext context)
    {
        AfterOrbChanneledRegistry.Run(context.CombatState.IterateHookListeners(), context);
    }

    public static void RunAfterOrbEvoked(AfterOrbEvokedHookContext context)
    {
        AfterOrbEvokedRegistry.Run(context.CombatState.IterateHookListeners(), context);
    }

    private static HookRegistry<AfterOrbChanneledHookContext> CreateAfterOrbChanneledRegistry()
    {
        var registry = new HookRegistry<AfterOrbChanneledHookContext>(AfterOrbChanneled);

        registry.Register<Metronome>(HandleMetronome);

        return registry;
    }

    private static HookRegistry<AfterOrbEvokedHookContext> CreateAfterOrbEvokedRegistry()
    {
        var registry = new HookRegistry<AfterOrbEvokedHookContext>(AfterOrbEvoked);

        registry.Register<ThunderPower>(HandleThunderPower);

        return registry;
    }

    private static void HandleThunderPower(ThunderPower power, AfterOrbEvokedHookContext context)
    {
        if (context.Orb.Owner != power.Owner.Player || context.Orb is not LightningOrb)
        {
            return;
        }

        var livingTargets = context.Targets
            .Where(creature => context.State.GetCreature(creature).IsAlive)
            .ToList();
        if (livingTargets.Count == 0)
        {
            return;
        }

        // Mirrors ThunderPower.AfterOrbEvoked damage. Flash/SFX/VFX/animation are omitted
        // because hover prediction only needs the combat-state effects.
        context.Simulator.Damage(livingTargets, power.Amount, ValueProp.Unpowered, power.Owner);
    }

    private static void HandleMetronome(Metronome relic, AfterOrbChanneledHookContext context)
    {
        if (context.Player != relic.Owner)
        {
            return;
        }

        var state = context.StateStore.Get(relic, () => new MetronomePredictionState
        {
            OrbsChanneled = relic._orbsChanneled
        });
        state.OrbsChanneled++;

        if (state.OrbsChanneled == relic.DynamicVars["OrbCount"].IntValue)
        {
            context.Simulator.Damage(context.State.HittableEnemies, relic.DynamicVars.Damage, relic.Owner.Creature);
        }
    }
}

internal sealed class MetronomePredictionState
{
    public int OrbsChanneled { get; set; }
}

internal sealed class AfterOrbChanneledHookContext : CombatPredictionHookContext
{
    public required Player Player { get; init; }

    public required OrbModel Orb { get; init; }
}

internal sealed class AfterOrbEvokedHookContext : CombatPredictionHookContext
{
    public required OrbModel Orb { get; init; }

    public required IReadOnlyList<Creature> Targets { get; init; }
}
