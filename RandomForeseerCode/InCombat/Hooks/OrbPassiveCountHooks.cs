using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using RandomForeseer.RandomForeseerCode.Common.Hooks;

namespace RandomForeseer.RandomForeseerCode.InCombat.Hooks;

internal static class OrbPassiveCountHooks
{
    private static readonly HookSpec ModifyOrbPassiveTriggerCountsSpec = new(
        nameof(AbstractModel.ModifyOrbPassiveTriggerCounts),
        [
            typeof(OrbModel),
            typeof(int)
        ]);

    private static readonly HookRegistry<OrbPassiveCountHookContext> ModifyOrbPassiveTriggerCountRegistry =
        CreateModifyOrbPassiveTriggerCountRegistry();

    public static int ModifyOrbPassiveTriggerCount(OrbPassiveCountHookContext context)
    {
        ModifyOrbPassiveTriggerCountRegistry.Run(context.CombatState.IterateHookListeners(), context);
        return context.TriggerCount;
    }

    private static HookRegistry<OrbPassiveCountHookContext> CreateModifyOrbPassiveTriggerCountRegistry()
    {
        var registry = new HookRegistry<OrbPassiveCountHookContext>(ModifyOrbPassiveTriggerCountsSpec);

        registry.Register<GoldPlatedCables>(HandleGoldPlatedCables);

        return registry;
    }

    private static void HandleGoldPlatedCables(GoldPlatedCables relic, OrbPassiveCountHookContext context)
    {
        if (context.Orb.Owner != relic.Owner ||
            !ReferenceEquals(context.State.GetPlayerCombatState(relic.Owner).OrbQueue.Orbs.FirstOrDefault(), context.Orb))
        {
            return;
        }

        context.TriggerCount++;
    }
}

internal sealed class OrbPassiveCountHookContext : CombatPredictionHookContext
{
    public required OrbModel Orb { get; init; }

    public int TriggerCount { get; set; } = 1;
}
