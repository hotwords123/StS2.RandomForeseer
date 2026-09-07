using System.Reflection;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.Common.Mirrors;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Hooks.Power;

using Registry = MethodMirrorRegistry<PowerModel, PowerApplicationMirrorContext>;

/// <summary>
/// Placeholder registries for model-specific PowerModel application lifecycle methods.
/// </summary>
internal static class PowerApplicationMirrors
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

    public static void InvokeBefore(PowerModel power, PowerApplicationMirrorContext context)
    {
        BeforeRegistry.Invoke(power, context);
    }

    public static void InvokeAfter(PowerModel power, PowerApplicationMirrorContext context)
    {
        AfterRegistry.Invoke(power, context);
    }
}

internal sealed class PowerApplicationMirrorContext : CombatMirrorContext<PowerModel>
{
    public required Creature Target { get; init; }

    public required decimal Amount { get; init; }

    public required Creature? Applier { get; init; }

    public required PredictedCard? CardSource { get; init; }
}
