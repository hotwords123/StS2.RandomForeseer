using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using RandomForeseer.RandomForeseerCode.Common.Mirrors;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Hooks.Damage;

using Registry = MethodMirrorRegistry<AbstractModel, ShouldAllowHittingMirrorContext, bool>;

internal static class ShouldAllowHittingMirrors
{
    private static readonly MirrorMethodSpec Method = MirrorMethodSpec.Hook(
        nameof(AbstractModel.ShouldAllowHitting), [typeof(Creature)]);

    private static readonly Registry Registry = CreateRegistry();

    public static bool Invoke(AbstractModel listener, ShouldAllowHittingMirrorContext context)
    {
        return Registry.TryInvokeRegistered(listener, context, out var result)
            ? result.Value
            : listener.ShouldAllowHitting(context.Creature);
    }

    private static Registry CreateRegistry()
    {
        var registry = new Registry(Method);
        registry.Register<DieForYouPower>(HandleDieForYouPower);
        return registry;
    }

    private static bool HandleDieForYouPower(DieForYouPower power, ShouldAllowHittingMirrorContext context)
    {
        return context.State.GetCreature(context.Creature).IsAlive;
    }
}

internal sealed class ShouldAllowHittingMirrorContext : CombatMirrorContext
{
    public required Creature Creature { get; init; }
}
