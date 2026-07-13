using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Models;
using RandomForeseer.RandomForeseerCode.Common.Mirrors;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Hooks.Attack;

using Registry = ModelMethodMirrorRegistry<AbstractModel, ModifyAttackHitCountMirrorContext, int>;

// Mirrors Hook.ModifyAttackHitCount while preserving listener-to-listener result chaining.
internal static class ModifyAttackHitCountMirrors
{
    private static readonly MirrorMethodSpec ModifyAttackHitCount = MirrorMethodSpec.Hook(
        nameof(AbstractModel.ModifyAttackHitCount),
        [typeof(AttackCommand), typeof(int)]);

    private static readonly Registry Registry = new(ModifyAttackHitCount);

    public static int Invoke(AbstractModel listener, ModifyAttackHitCountMirrorContext context)
    {
        return Registry.Invoke(listener, context, context.HitCount).Value;
    }
}

internal sealed class ModifyAttackHitCountMirrorContext : CombatPredictionMirrorContext
{
    public required AttackCommand Command { get; init; }

    public required int HitCount { get; set; }
}
