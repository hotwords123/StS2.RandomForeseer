using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.Common.Mirrors;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Hooks.Block;

using Registry = ModelMethodMirrorRegistry<AbstractModel, BeforeBlockGainedMirrorContext>;

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

internal sealed class BeforeBlockGainedMirrorContext : CombatPredictionMirrorContext
{
    public required Creature Creature { get; init; }

    public required decimal Amount { get; init; }

    public required ValueProp Props { get; init; }

    public required PredictedCard? Source { get; init; }
}
