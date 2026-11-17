using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using RandomForeseer.Common.Mirrors;

namespace RandomForeseer.InCombat.Mirrors.Hooks.Damage;

using Registry = MethodMirrorRegistry<AbstractModel, ModifyPreBlockDamageTargetMirrorContext>;

internal static class ModifyPreBlockDamageTargetMirrors
{
    private static readonly MirrorMethodSpec Method = MirrorMethodSpec.Hook(
        nameof(AbstractModel.ModifyPreBlockDamageTarget),
        [typeof(Creature), typeof(Creature), typeof(decimal), typeof(ValueProp), typeof(Creature)]);

    private static readonly Registry Registry = CreateRegistry();

    public static void Invoke(AbstractModel listener, ModifyPreBlockDamageTargetMirrorContext context)
    {
        Registry.Invoke(listener, context);
    }

    private static Registry CreateRegistry()
    {
        var registry = new Registry(Method);
        registry.Register<WuppysLoyaltyPower>(HandleWuppysLoyaltyPower);
        return registry;
    }

    private static void HandleWuppysLoyaltyPower(
        WuppysLoyaltyPower power,
        ModifyPreBlockDamageTargetMirrorContext context)
    {
        if (context.OriginalTarget == power.Owner.PetOwner?.Creature &&
            context.State.GetCreature(power.Owner).IsAlive && context.Props.IsPoweredAttack())
        {
            context.Target = power.Owner;
        }
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
