using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using RandomForeseer.Common.Mirrors;

namespace RandomForeseer.InCombat.Mirrors.Hooks.Damage;

using Registry = MethodMirrorRegistry<AbstractModel, AfterModifyingHpLostMirrorContext>;

internal static class AfterModifyingHpLostAfterOstyMirrors
{
    private static readonly MirrorMethodSpec AfterModifyingHpLostAfterOsty = MirrorMethodSpec.Hook(
        nameof(AbstractModel.AfterModifyingHpLostAfterOsty),
        []);

    private static readonly Registry Registry = CreateRegistry();

    public static void Invoke(AbstractModel modifier, AfterModifyingHpLostMirrorContext context)
    {
        Registry.Invoke(modifier, context);
    }

    private static Registry CreateRegistry()
    {
        var registry = new Registry(AfterModifyingHpLostAfterOsty);

        registry.RegisterIgnored<BeatingRemnant>();
        registry.Register<BufferPower>(HandleBufferPower);
        registry.RegisterIgnored<IntangiblePower>();
        registry.RegisterIgnored<TheBoot>();
        registry.RegisterIgnored<TungstenRod>();

        return registry;
    }

    private static void HandleBufferPower(BufferPower power, AfterModifyingHpLostMirrorContext context)
    {
        context.StateStore.GetPowerAmount(power).Decrement();
    }
}

internal sealed class AfterModifyingHpLostMirrorContext : CombatMirrorContext
{
}
