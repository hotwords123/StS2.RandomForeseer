using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using RandomForeseer.Common.Mirrors;

namespace RandomForeseer.InCombat.Mirrors.Hooks.Damage;

// The new pre-block pet domain is deferred. Detect overrides through the ordinary registry so
// predictions surface a risk instead of calling a live-state redirection method or silently omitting it.
internal static class ModifyPreBlockDamageTargetMirrors
{
    private static readonly MethodMirrorRegistry<AbstractModel, ModifyPreBlockDamageTargetMirrorContext> Registry = new(
        MirrorMethodSpec.Hook(
            nameof(AbstractModel.ModifyPreBlockDamageTarget),
            [typeof(Creature), typeof(Creature), typeof(decimal), typeof(ValueProp), typeof(Creature)]));

    public static void Invoke(AbstractModel listener, ModifyPreBlockDamageTargetMirrorContext context)
    {
        Registry.Invoke(listener, context);
    }
}

internal sealed class ModifyPreBlockDamageTargetMirrorContext : CombatMirrorContext
{
    public required Creature OriginalTarget { get; init; }

    public Creature? Target { get; set; }

    public required decimal Amount { get; init; }

    public required ValueProp Props { get; init; }

    public required Creature? Dealer { get; init; }
}
