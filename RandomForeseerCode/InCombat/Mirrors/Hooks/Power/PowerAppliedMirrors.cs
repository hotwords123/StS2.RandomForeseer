using System.Reflection;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.Common.Mirrors;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Hooks.Power;

using Registry = MethodMirrorRegistry<PowerModel, PowerAppliedMirrorContext>;

/// <summary>
/// Placeholder registries for model-specific PowerModel application lifecycle methods.
/// </summary>
internal static class PowerAppliedMirrors
{
    private static readonly MirrorMethodSpec BeforeApplied = new(
        typeof(PowerModel),
        nameof(PowerModel.BeforeApplied),
        BindingFlags.Instance | BindingFlags.Public,
        [typeof(Creature), typeof(decimal), typeof(Creature), typeof(CardModel)]);

    private static readonly MirrorMethodSpec AfterApplied = new(
        typeof(PowerModel),
        nameof(PowerModel.AfterApplied),
        BindingFlags.Instance | BindingFlags.Public,
        [typeof(Creature), typeof(CardModel)]);

    private static readonly Registry BeforeRegistry = new(BeforeApplied);
    private static readonly Registry AfterRegistry = new(AfterApplied);

    public static void InvokeBefore(
        CombatPredictionSimulator simulator,
        PowerModel power,
        Creature target,
        decimal amount,
        Creature? applier,
        PredictedCard? cardSource)
    {
        BeforeRegistry.Invoke(power, new()
        {
            Simulator = simulator,
            Target = target,
            Amount = amount,
            Applier = applier,
            CardSource = cardSource
        });
    }

    public static void InvokeAfter(
        CombatPredictionSimulator simulator,
        PowerModel power,
        Creature target,
        decimal amount,
        Creature? applier,
        PredictedCard? cardSource)
    {
        AfterRegistry.Invoke(power, new()
        {
            Simulator = simulator,
            Target = target,
            Amount = amount,
            Applier = applier,
            CardSource = cardSource
        });
    }
}

internal sealed class PowerAppliedMirrorContext : CombatMirrorContext<PowerModel>
{
    public required Creature Target { get; init; }

    public required decimal Amount { get; init; }

    public required Creature? Applier { get; init; }

    public required PredictedCard? CardSource { get; init; }
}
