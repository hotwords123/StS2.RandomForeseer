using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.ValueProps;
using RandomForeseer.RandomForeseerCode.Common.Hooks;

namespace RandomForeseer.RandomForeseerCode.InCombat.Hooks;

internal static class AfterCurrentHpChangedHook
{
    private static readonly HookSpec AfterCurrentHpChanged = new(
        nameof(AbstractModel.AfterCurrentHpChanged),
        [typeof(Creature), typeof(decimal)]);

    private static readonly HookRegistry<AfterCurrentHpChangedHookContext> Registry = CreateRegistry();

    public static void Run(AfterCurrentHpChangedHookContext context)
    {
        Registry.Run(context.RunState.IterateHookListeners(context.CombatState), context);
    }

    private static HookRegistry<AfterCurrentHpChangedHookContext> CreateRegistry()
    {
        var registry = new HookRegistry<AfterCurrentHpChangedHookContext>(AfterCurrentHpChanged);

        registry.RegisterIgnored<Crusher>();
        registry.RegisterIgnored<Rocket>();
        registry.Register<RedSkull>(HandleRedSkull);
        registry.Register<NecroMasteryPower>(HandleNecroMasteryPower);
        registry.RegisterIgnored<MeatOnTheBone>();

        return registry;
    }

    private static void HandleRedSkull(RedSkull relic, AfterCurrentHpChangedHookContext context)
    {
        var ownerState = context.State.GetCreature(relic.Owner.Creature);
        var threshold = ownerState.MaxHp * (relic.DynamicVars["HpThreshold"].BaseValue / 100m);
        var shouldApplyStrength = ownerState.CurrentHp <= threshold;
        if (shouldApplyStrength != relic._strengthApplied)
        {
            context.MarkCurrentSourceRisky();
        }
    }

    private static void HandleNecroMasteryPower(NecroMasteryPower power, AfterCurrentHpChangedHookContext context)
    {
        if (context.Delta >= 0m ||
            context.Creature.Monster is not Osty ||
            context.Creature.PetOwner != power.Owner.Player)
        {
            return;
        }

        context.Simulator.Damage(
            context.State.HittableEnemies,
            -context.Delta * power.Amount,
            ValueProp.Unblockable | ValueProp.Unpowered,
            power.Owner);
    }
}

internal sealed class AfterCurrentHpChangedHookContext : CombatPredictionHookContext
{
    public required Creature Creature { get; init; }

    public required decimal Delta { get; init; }
}
