using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using RandomForeseer.RandomForeseerCode.Common.Mirrors;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Hooks.Orb;

using Registry = ModelMethodMirrorRegistry<AbstractModel, ModifyOrbPassiveTriggerCountMirrorContext, int>;

internal static class ModifyOrbPassiveTriggerCountMirrors
{
    private static readonly MirrorMethodSpec Method = MirrorMethodSpec.Hook(
        nameof(AbstractModel.ModifyOrbPassiveTriggerCounts),
        [typeof(OrbModel), typeof(int)]);

    private static readonly Registry Registry = CreateRegistry();

    public static int Invoke(AbstractModel listener, ModifyOrbPassiveTriggerCountMirrorContext context)
    {
        return Registry.Invoke(listener, context, context.TriggerCount).Value;
    }

    private static Registry CreateRegistry()
    {
        var registry = new Registry(Method);
        registry.Register<GoldPlatedCables>(HandleGoldPlatedCables);
        return registry;
    }

    // Mirrors GoldPlatedCables.ModifyOrbPassiveTriggerCounts against the shadow orb queue.
    private static int HandleGoldPlatedCables(
        GoldPlatedCables relic,
        ModifyOrbPassiveTriggerCountMirrorContext context)
    {
        if (context.Orb.Owner == relic.Owner &&
            context.State.GetPlayerCombatState(relic.Owner).OrbQueue.Orbs is [var firstOrb, ..] &&
            context.Orb == firstOrb)
        {
            return context.TriggerCount + 1;
        }

        return context.TriggerCount;
    }
}

internal sealed class ModifyOrbPassiveTriggerCountMirrorContext : CombatPredictionMirrorContext
{
    public required OrbModel Orb { get; init; }

    public required int TriggerCount { get; set; }
}
