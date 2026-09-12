using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using RandomForeseer.RandomForeseerCode.Common.Mirrors;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Hooks.Damage;

using Registry = MethodMirrorRegistry<AbstractModel, ModifyUnblockedDamageTargetMirrorContext, Creature>;

internal static class ModifyUnblockedDamageTargetMirrors
{
    private static readonly MirrorMethodSpec Method = MirrorMethodSpec.Hook(
        nameof(AbstractModel.ModifyUnblockedDamageTarget),
        [typeof(Creature), typeof(decimal), typeof(ValueProp), typeof(Creature)]);

    private static readonly Registry Registry = CreateRegistry();

    public static Creature Invoke(AbstractModel listener, ModifyUnblockedDamageTargetMirrorContext context)
    {
        return Registry.TryInvokeRegistered(listener, context, out var result)
            ? result.Value
            : listener.ModifyUnblockedDamageTarget(context.Target, context.Amount, context.Props, context.Dealer);
    }

    private static Registry CreateRegistry()
    {
        var registry = new Registry(Method);
        registry.Register<DieForYouPower>(HandleDieForYouPower);
        return registry;
    }

    private static Creature HandleDieForYouPower(
        DieForYouPower power,
        ModifyUnblockedDamageTargetMirrorContext context)
    {
        return context.Target == power.Owner.PetOwner?.Creature &&
            context.State.GetCreature(power.Owner).IsAlive && context.Props.IsPoweredAttack()
                ? power.Owner
                : context.Target;
    }
}

internal sealed class ModifyUnblockedDamageTargetMirrorContext : CombatMirrorContext
{
    public required Creature Target { get; set; }

    public required decimal Amount { get; init; }

    public required ValueProp Props { get; init; }

    public required Creature? Dealer { get; init; }
}
