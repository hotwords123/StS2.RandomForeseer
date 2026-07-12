using System.Reflection;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Orbs;
using RandomForeseer.RandomForeseerCode.Common.Mirrors;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Orbs;

using OrbPassiveRegistry = ModelMethodMirrorRegistry<OrbModel, OrbPassiveMirrorContext>;
using OrbEvokeRegistry = ModelMethodMirrorRegistry<OrbModel, OrbMirrorContext, IReadOnlyList<Creature>>;
using OrbTriggerRegistry = ModelMethodMirrorRegistry<OrbModel, OrbMirrorContext>;

// Simulation-facing facade and central registration index for mirrored OrbModel behavior.
internal static class OrbMirrors
{
    private static readonly MirrorMethodSpec Passive = new(
        typeof(OrbModel),
        nameof(OrbModel.Passive),
        BindingFlags.Instance | BindingFlags.Public,
        [typeof(PlayerChoiceContext), typeof(Creature)]);

    private static readonly MirrorMethodSpec Evoke = new(
        typeof(OrbModel),
        nameof(OrbModel.Evoke),
        BindingFlags.Instance | BindingFlags.Public,
        [typeof(PlayerChoiceContext)]);

    private static readonly MirrorMethodSpec BeforeTurnEndOrbTrigger = new(
        typeof(OrbModel),
        nameof(OrbModel.BeforeTurnEndOrbTrigger),
        BindingFlags.Instance | BindingFlags.Public,
        [typeof(PlayerChoiceContext)]);

    private static readonly OrbPassiveRegistry PassiveRegistry = CreatePassiveRegistry();
    private static readonly OrbEvokeRegistry EvokeRegistry = CreateEvokeRegistry();
    private static readonly OrbTriggerRegistry BeforeTurnEndOrbTriggerRegistry =
        CreateBeforeTurnEndOrbTriggerRegistry();

    public static void InvokePassive(
        CombatPredictionSimulator simulator,
        OrbModel orb,
        Creature? target = null)
    {
        PassiveRegistry.Invoke(orb, new() { Simulator = simulator, Target = target });
    }

    public static IReadOnlyList<Creature> InvokeEvoke(
        CombatPredictionSimulator simulator,
        OrbModel orb)
    {
        return EvokeRegistry.Invoke(orb, new() { Simulator = simulator }, []).Value;
    }

    public static void InvokeBeforeTurnEndOrbTrigger(
        CombatPredictionSimulator simulator,
        OrbModel orb)
    {
        BeforeTurnEndOrbTriggerRegistry.Invoke(orb, new() { Simulator = simulator });
    }

    private static OrbPassiveRegistry CreatePassiveRegistry()
    {
        var registry = new OrbPassiveRegistry(Passive);

        registry.Register<LightningOrb>(LightningOrbMirrors.Passive);
        registry.Register<FrostOrb>(FrostOrbMirrors.Passive);
        registry.Register<DarkOrb>(DarkOrbMirrors.Passive);
        registry.Register<GlassOrb>(GlassOrbMirrors.Passive);
        registry.Register<PlasmaOrb>(PlasmaOrbMirrors.Passive);

        return registry;
    }

    private static OrbEvokeRegistry CreateEvokeRegistry()
    {
        var registry = new OrbEvokeRegistry(Evoke);

        registry.Register<LightningOrb>(LightningOrbMirrors.Evoke);
        registry.Register<FrostOrb>(FrostOrbMirrors.Evoke);
        registry.Register<DarkOrb>(DarkOrbMirrors.Evoke);
        registry.Register<GlassOrb>(GlassOrbMirrors.Evoke);
        registry.Register<PlasmaOrb>(PlasmaOrbMirrors.Evoke);

        return registry;
    }

    private static OrbTriggerRegistry CreateBeforeTurnEndOrbTriggerRegistry()
    {
        var registry = new OrbTriggerRegistry(BeforeTurnEndOrbTrigger);

        registry.Register<LightningOrb>(LightningOrbMirrors.BeforeTurnEndOrbTrigger);
        registry.Register<FrostOrb>(FrostOrbMirrors.BeforeTurnEndOrbTrigger);
        registry.Register<DarkOrb>(DarkOrbMirrors.BeforeTurnEndOrbTrigger);
        registry.Register<GlassOrb>(GlassOrbMirrors.BeforeTurnEndOrbTrigger);

        return registry;
    }
}
