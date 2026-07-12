using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using RandomForeseer.RandomForeseerCode.Common.Mirrors;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Hooks.Orb;

using Registry = ModelMethodMirrorRegistry<AbstractModel, AfterOrbChanneledMirrorContext>;

internal static class AfterOrbChanneledMirrors
{
    private static readonly MirrorMethodSpec Method = MirrorMethodSpec.Hook(
        nameof(AbstractModel.AfterOrbChanneled),
        [typeof(PlayerChoiceContext), typeof(Player), typeof(OrbModel)]);

    private static readonly Registry Registry = CreateRegistry();

    public static void Invoke(AbstractModel listener, AfterOrbChanneledMirrorContext context)
    {
        Registry.Invoke(listener, context);
    }

    private static Registry CreateRegistry()
    {
        var registry = new Registry(Method);
        registry.Register<Metronome>(HandleMetronome);
        return registry;
    }

    private static void HandleMetronome(Metronome relic, AfterOrbChanneledMirrorContext context)
    {
        if (context.Player != relic.Owner)
        {
            return;
        }

        var state = context.StateStore.Get(relic, () => new MetronomePredictionState(relic));
        state.OrbsChanneled++;

        if (state.OrbsChanneled == relic.DynamicVars[Metronome._orbCountKey].IntValue)
        {
            context.Simulator.Damage(
                context.State.HittableEnemies,
                relic.DynamicVars.Damage,
                relic.Owner.Creature);
        }
    }
}

internal sealed class AfterOrbChanneledMirrorContext : CombatPredictionMirrorContext
{
    public required Player Player { get; init; }

    public required OrbModel Orb { get; init; }
}

internal sealed class MetronomePredictionState(Metronome relic)
{
    public int OrbsChanneled { get; set; } = relic._orbsChanneled;
}
