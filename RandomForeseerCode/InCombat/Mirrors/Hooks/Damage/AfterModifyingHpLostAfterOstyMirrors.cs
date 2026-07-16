using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.Common.Mirrors;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Hooks.Damage;

using Registry = ModelMethodMirrorRegistry<AbstractModel, AfterModifyingHpLostMirrorContext>;

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
        // Vanilla decrements Buffer here. The simulator cannot shadow that into the original
        // ModifyHpLostAfterOstyLate hook without mutating the live power, so surface a warning.
        context.History.RecordRisk(PredictionRiskReason.MethodMirrorIncomplete);
    }
}

internal sealed class AfterModifyingHpLostMirrorContext : CombatPredictionMirrorContext
{
}
