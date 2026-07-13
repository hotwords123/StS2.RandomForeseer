using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.Common.Mirrors;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Hooks.Block;

using Registry = ModelMethodMirrorRegistry<AbstractModel, AfterBlockGainedMirrorContext>;

// Mirrors the prediction-relevant parts of Hook.AfterBlockGained.
internal static class AfterBlockGainedMirrors
{
    private static readonly MirrorMethodSpec AfterBlockGained = MirrorMethodSpec.Hook(
        nameof(AbstractModel.AfterBlockGained),
        [
            typeof(Creature),
            typeof(decimal),
            typeof(ValueProp),
            typeof(CardModel)
        ]);

    private static readonly Registry Registry = CreateRegistry();

    public static void Invoke(AbstractModel listener, AfterBlockGainedMirrorContext context)
    {
        Registry.Invoke(listener, context);
    }

    private static Registry CreateRegistry()
    {
        var registry = new Registry(AfterBlockGained);

        registry.Register<BeaconOfHopePower>(HandleBeaconOfHopePower);
        registry.Register<JuggernautPower>(HandleJuggernautPower);

        return registry;
    }

    private static void HandleBeaconOfHopePower(BeaconOfHopePower power, AfterBlockGainedMirrorContext context)
    {
        var state = context.StateStore.Get<BeaconOfHopePredictionState>(power);
        if (context.Amount < 1m ||
            context.Creature != power.Owner ||
            context.CombatState.CurrentSide != power.Owner.Side ||
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

    private static void HandleJuggernautPower(JuggernautPower power, AfterBlockGainedMirrorContext context)
    {
        if (context.Creature == power.Owner && context.Amount > 0m)
        {
            var target = context.Rng.CombatTargets.NextItem(context.State.HittableEnemies);
            if (target is not null)
            {
                context.Simulator.Damage(target, power.Amount, ValueProp.Unpowered, power.Owner);
            }
        }
    }
}

internal sealed class AfterBlockGainedMirrorContext : CombatPredictionMirrorContext
{
    public required Creature Creature { get; init; }

    public required decimal Amount { get; init; }

    public required ValueProp Props { get; init; }

    public required PredictedCard? Source { get; init; }
}

internal sealed class BeaconOfHopePredictionState
{
    public bool HasAlreadyBeenGivenBlock { get; set; }
}
