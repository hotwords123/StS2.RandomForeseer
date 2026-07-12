using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Orbs;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using RandomForeseer.RandomForeseerCode.Common.Mirrors;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Hooks.Orb;

using Registry = ModelMethodMirrorRegistry<AbstractModel, AfterOrbEvokedMirrorContext>;

internal static class AfterOrbEvokedMirrors
{
    private static readonly MirrorMethodSpec Method = MirrorMethodSpec.Hook(
        nameof(AbstractModel.AfterOrbEvoked),
        [typeof(PlayerChoiceContext), typeof(OrbModel), typeof(IEnumerable<Creature>)]);

    private static readonly Registry Registry = CreateRegistry();

    public static void Invoke(AbstractModel listener, AfterOrbEvokedMirrorContext context)
    {
        Registry.Invoke(listener, context);
    }

    private static Registry CreateRegistry()
    {
        var registry = new Registry(Method);
        registry.Register<ThunderPower>(HandleThunderPower);
        return registry;
    }

    // Mirrors ThunderPower.AfterOrbEvoked damage; presentation effects are omitted.
    private static void HandleThunderPower(ThunderPower power, AfterOrbEvokedMirrorContext context)
    {
        if (context.Orb.Owner != power.Owner.Player || context.Orb is not LightningOrb)
        {
            return;
        }

        var livingTargets = context.Targets
            .Where(creature => context.State.GetCreature(creature).IsAlive)
            .ToList();
        if (livingTargets.Count > 0)
        {
            context.Simulator.Damage(livingTargets, power.Amount, ValueProp.Unpowered, power.Owner);
        }
    }
}

internal sealed class AfterOrbEvokedMirrorContext : CombatPredictionMirrorContext
{
    public required OrbModel Orb { get; init; }

    public required IReadOnlyList<Creature> Targets { get; init; }
}
