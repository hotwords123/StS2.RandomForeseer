using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using RandomForeseer.Common;
using RandomForeseer.Common.Mirrors;
using RandomForeseer.InCombat.Simulation;

namespace RandomForeseer.InCombat.Mirrors.Hooks.Power;

using Registry = MethodMirrorRegistry<PowerModel, PowerAppliedMirrorContext>;

/// <summary>
/// Mirrors model-specific PowerModel application lifecycle methods.
/// </summary>
internal static class PowerAppliedMirrors
{
    private static readonly MirrorMethodSpec BeforeApplied = new(
        typeof(PowerModel),
        nameof(PowerModel.BeforeApplied),
        ReflectionFlags.PublicInstance,
        [typeof(Creature), typeof(decimal), typeof(Creature), typeof(CardModel)]);

    private static readonly MirrorMethodSpec AfterApplied = new(
        typeof(PowerModel),
        nameof(PowerModel.AfterApplied),
        ReflectionFlags.PublicInstance,
        [typeof(Creature), typeof(CardModel)]);

    private static readonly Registry BeforeRegistry = new(BeforeApplied);
    private static readonly Registry AfterRegistry = CreateAfterRegistry();

    private static Registry CreateAfterRegistry()
    {
        var registry = new Registry(AfterApplied);
        registry.RegisterIgnored<PhantomBladesPower>();
        return registry;
    }

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
