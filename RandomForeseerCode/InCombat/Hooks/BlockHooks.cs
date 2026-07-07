using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using RandomForeseer.RandomForeseerCode.Common.Hooks;

namespace RandomForeseer.RandomForeseerCode.InCombat.Hooks;

internal static class BlockHooks
{
    private static readonly HookSpec BeforeBlockGained = new(
        nameof(AbstractModel.BeforeBlockGained),
        [
            typeof(Creature),
            typeof(decimal),
            typeof(ValueProp),
            typeof(CardModel)
        ]);

    private static readonly HookSpec AfterBlockGained = new(
        nameof(AbstractModel.AfterBlockGained),
        [
            typeof(Creature),
            typeof(decimal),
            typeof(ValueProp),
            typeof(CardModel)
        ]);

    private static readonly HookRegistry<BlockHookContext> BeforeBlockGainedRegistry = new(BeforeBlockGained);
    private static readonly HookRegistry<BlockHookContext> AfterBlockGainedRegistry = CreateAfterBlockGainedRegistry();

    public static void RunBeforeBlockGained(BlockHookContext context)
    {
        BeforeBlockGainedRegistry.Run(context.CombatState.IterateHookListeners(), context);
    }

    public static void RunAfterBlockGained(BlockHookContext context)
    {
        AfterBlockGainedRegistry.Run(context.CombatState.IterateHookListeners(), context);
    }

    private static HookRegistry<BlockHookContext> CreateAfterBlockGainedRegistry()
    {
        var registry = new HookRegistry<BlockHookContext>(AfterBlockGained);

        registry.Register<BeaconOfHopePower>(HandleBeaconOfHopePower);
        registry.Register<JuggernautPower>(HandleJuggernautPower);

        return registry;
    }

    private static void HandleBeaconOfHopePower(BeaconOfHopePower power, BlockHookContext context)
    {
        var state = context.StateStore.Get<BeaconOfHopePredictionState>(power);
        if (context.Creature != power.Owner ||
            context.CombatState.CurrentSide != power.Owner.Side ||
            context.Amount < 2m ||
            state.HasAlreadyBeenGivenBlock)
        {
            return;
        }

        var amountToGive = context.Amount * 0.5m;
        if (amountToGive < 1m)
        {
            return;
        }

        var teammates = context.CombatState.GetTeammatesOf(power.Owner)
            .Where(creature => creature is { IsAlive: true, IsPlayer: true } && creature != power.Owner)
            .ToList();
        if (teammates.Count == 0)
        {
            return;
        }

        state.HasAlreadyBeenGivenBlock = true;
        try
        {
            foreach (var teammate in teammates)
            {
                context.Simulator.GainBlock(teammate, amountToGive, ValueProp.Unpowered);
            }
        }
        finally
        {
            state.HasAlreadyBeenGivenBlock = false;
        }
    }

    private static void HandleJuggernautPower(JuggernautPower power, BlockHookContext context)
    {
        if (context.Creature != power.Owner || context.Amount <= 0m)
        {
            return;
        }

        var target = context.Rng.CombatTargets.NextItem(context.State.HittableEnemies);
        if (target != null)
        {
            context.Simulator.Damage(target, power.Amount, ValueProp.Unpowered, power.Owner);
        }
    }
}

internal sealed class BeaconOfHopePredictionState
{
    public bool HasAlreadyBeenGivenBlock { get; set; }
}

internal sealed class BlockHookContext : CombatPredictionHookContext
{
    public required Creature Creature { get; init; }

    public required decimal Amount { get; init; }
}
