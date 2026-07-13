using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.ValueProps;
using RandomForeseer.RandomForeseerCode.Common.Mirrors;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Hooks.Damage;

using Registry = ModelMethodMirrorRegistry<AbstractModel, AfterCurrentHpChangedMirrorContext>;

internal static class AfterCurrentHpChangedMirrors
{
    private static readonly MirrorMethodSpec AfterCurrentHpChanged = MirrorMethodSpec.Hook(
        nameof(AbstractModel.AfterCurrentHpChanged),
        [typeof(Creature), typeof(decimal)]);

    private static readonly Registry Registry = CreateRegistry();

    public static void Invoke(AbstractModel listener, AfterCurrentHpChangedMirrorContext context)
    {
        Registry.Invoke(listener, context);
    }

    private static Registry CreateRegistry()
    {
        var registry = new Registry(AfterCurrentHpChanged);

        registry.RegisterIgnored<Crusher>();
        registry.RegisterIgnored<Rocket>();
        registry.Register<RedSkull>(HandleRedSkull);
        registry.Register<NecroMasteryPower>(HandleNecroMasteryPower);
        registry.RegisterIgnored<MeatOnTheBone>();

        return registry;
    }

    private static void HandleRedSkull(RedSkull relic, AfterCurrentHpChangedMirrorContext context)
    {
        var ownerState = context.State.GetCreature(relic.Owner.Creature);
        var threshold = ownerState.MaxHp * (relic.DynamicVars[RedSkull._hpThresholdKey].BaseValue / 100m);
        var shouldApplyStrength = ownerState.CurrentHp <= threshold;
        if (shouldApplyStrength != relic._strengthApplied)
        {
            context.MarkCurrentSourceRisky();
        }
    }

    private static void HandleNecroMasteryPower(NecroMasteryPower power, AfterCurrentHpChangedMirrorContext context)
    {
        if (context.Delta < 0m &&
            context.Creature.Monster is Osty &&
            context.Creature.PetOwner == power.Owner.Player)
        {
            context.Simulator.Damage(
                context.State.HittableEnemies,
                -context.Delta * power.Amount,
                ValueProp.Unblockable | ValueProp.Unpowered,
                power.Owner);
        }
    }
}

internal sealed class AfterCurrentHpChangedMirrorContext : CombatPredictionMirrorContext
{
    public required Creature Creature { get; init; }

    public required decimal Delta { get; init; }
}
