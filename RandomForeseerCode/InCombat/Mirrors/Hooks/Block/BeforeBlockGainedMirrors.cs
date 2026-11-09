using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using RandomForeseer.Common;
using RandomForeseer.Common.Mirrors;

namespace RandomForeseer.InCombat.Mirrors.Hooks.Block;

using Registry = MethodMirrorRegistry<AbstractModel, BeforeBlockGainedMirrorContext>;

// Mirrors Hook.BeforeBlockGained.
internal static class BeforeBlockGainedMirrors
{
    private static readonly MirrorMethodSpec BeforeBlockGained = MirrorMethodSpec.Hook(
        nameof(AbstractModel.BeforeBlockGained),
        [
            typeof(Creature),
            typeof(decimal),
            typeof(ValueProp),
            typeof(CardModel)
        ]);

    private static readonly Registry Registry = new(BeforeBlockGained);

    public static void Invoke(AbstractModel listener, BeforeBlockGainedMirrorContext context)
    {
        Registry.Invoke(listener, context);
    }
}

internal sealed class BeforeBlockGainedMirrorContext : CombatMirrorContext
{
    public required Creature Creature { get; init; }

    public required decimal Amount { get; init; }

    public required ValueProp Props { get; init; }

    public required PredictedCard? Source { get; init; }
}
