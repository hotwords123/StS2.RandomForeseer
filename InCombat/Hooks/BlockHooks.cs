using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using RandomForeseer.Common;
using RandomForeseer.Common.Hooks;

namespace RandomForeseer.InCombat.Hooks;

internal static class BlockHooks
{
    private static readonly HookSpec AfterBlockGained = new(
        nameof(AbstractModel.AfterBlockGained),
        [
            typeof(Creature),
            typeof(decimal),
            typeof(ValueProp),
            typeof(CardModel)
        ]);

    private static readonly HookRegistry<BlockHookContext> AfterBlockGainedRegistry = CreateAfterBlockGainedRegistry();

    public static IReadOnlyList<HookResult> RunAfterBlockGained(BlockHookContext context)
    {
        return AfterBlockGainedRegistry.Run(context.CombatState.IterateHookListeners(), context);
    }

    private static HookRegistry<BlockHookContext> CreateAfterBlockGainedRegistry()
    {
        var registry = new HookRegistry<BlockHookContext>(AfterBlockGained);

        registry.Register<BeaconOfHopePower>(HandleBeaconOfHopePower);
        registry.Register<JuggernautPower>(HandleJuggernautPower);

        return registry;
    }

    private static HookResultKind HandleBeaconOfHopePower(BeaconOfHopePower power, BlockHookContext context)
    {
        var state = context.StateStore.Get<BeaconOfHopePredictionState>(power);
        if (context.Creature != power.Owner ||
            context.CombatState.CurrentSide != power.Owner.Side ||
            context.Amount < 2m ||
            state.HasAlreadyBeenGivenBlock)
        {
            return HookResultKind.Ignored;
        }

        var amountToGive = context.Amount * 0.5m;
        if (amountToGive < 1m)
        {
            return HookResultKind.Ignored;
        }

        var teammates = context.CombatState.GetTeammatesOf(power.Owner)
            .Where(creature => creature is { IsAlive: true, IsPlayer: true } && creature != power.Owner)
            .ToList();
        if (teammates.Count == 0)
        {
            return HookResultKind.Ignored;
        }

        state.HasAlreadyBeenGivenBlock = true;
        try
        {
            foreach (var teammate in teammates)
            {
                context.Executor.GainBlock(teammate, amountToGive, ValueProp.Unpowered);
            }
        }
        finally
        {
            state.HasAlreadyBeenGivenBlock = false;
        }

        return HookResultKind.Applied;
    }

    private static HookResultKind HandleJuggernautPower(JuggernautPower power, BlockHookContext context)
    {
        if (context.Creature != power.Owner || context.CombatState.HittableEnemies.Count == 0)
        {
            return HookResultKind.Ignored;
        }

        if (context.CombatState.HittableEnemies.Count > 1)
        {
            return HookResultKind.DriftRisk;
        }

        context.Executor.Damage(
            context.CombatState.HittableEnemies,
            power.Amount,
            ValueProp.Unpowered,
            power.Owner);
        return HookResultKind.Applied;
    }
}

internal sealed class BeaconOfHopePredictionState
{
    public bool HasAlreadyBeenGivenBlock { get; set; }
}

internal sealed class BlockHookContext
{
    public required IDamageBlockExecutor Executor { get; init; }

    public required PredictionStateStore StateStore { get; init; }

    public required ICombatState CombatState { get; init; }

    public required Creature Creature { get; init; }

    public required decimal Amount { get; init; }

    public required ValueProp Props { get; init; }

    public required CardModel? Source { get; init; }
}
