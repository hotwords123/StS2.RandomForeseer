using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using RandomForeseer.RandomForeseerCode.Common.Hooks;

namespace RandomForeseer.RandomForeseerCode.InCombat.Hooks;

internal static class DamageModifierHooks
{
    private static readonly HookSpec AfterModifyingHpLostAfterOsty = new(
        nameof(AbstractModel.AfterModifyingHpLostAfterOsty),
        []);

    private static readonly HookRegistry<AfterModifyingHpLostHookContext> AfterModifyingHpLostAfterOstyRegistry =
        CreateAfterModifyingHpLostAfterOstyRegistry();

    public static void RunAfterModifyingHpLostAfterOsty(
        IEnumerable<AbstractModel> modifiers,
        AfterModifyingHpLostHookContext context)
    {
        AfterModifyingHpLostAfterOstyRegistry.Run(modifiers, context);
    }

    private static HookRegistry<AfterModifyingHpLostHookContext> CreateAfterModifyingHpLostAfterOstyRegistry()
    {
        var registry = new HookRegistry<AfterModifyingHpLostHookContext>(AfterModifyingHpLostAfterOsty);

        registry.RegisterIgnored<BeatingRemnant>();
        registry.Register<BufferPower>(HandleBufferPower);
        registry.RegisterIgnored<IntangiblePower>();
        registry.RegisterIgnored<TheBoot>();
        registry.RegisterIgnored<TungstenRod>();

        return registry;
    }

    private static void HandleBufferPower(BufferPower power, AfterModifyingHpLostHookContext context)
    {
        // Vanilla decrements Buffer here. The simulator cannot shadow that into the original
        // ModifyHpLostAfterOstyLate hook without mutating the live power, so surface a warning.
        context.MarkCurrentSourceRisky();
    }
}

internal sealed class AfterModifyingHpLostHookContext : CombatPredictionHookContext
{
}
