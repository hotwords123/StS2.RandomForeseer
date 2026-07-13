using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Models.Relics;
using RandomForeseer.RandomForeseerCode.Common.Mirrors;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Hooks.Death;

using Registry = ModelMethodMirrorRegistry<AbstractModel, ShouldDieMirrorContext, bool>;

// Mirrors Hook.ShouldDie's normal and late predicate phases.
internal static class ShouldDieMirrors
{
    private static readonly MirrorMethodSpec ShouldDie = MirrorMethodSpec.Hook(
        nameof(AbstractModel.ShouldDie),
        [typeof(Creature)]);

    private static readonly MirrorMethodSpec ShouldDieLate = MirrorMethodSpec.Hook(
        nameof(AbstractModel.ShouldDieLate),
        [typeof(Creature)]);

    private static readonly Registry Registry = CreateRegistry();
    private static readonly Registry LateRegistry = CreateLateRegistry();

    public static bool Invoke(AbstractModel listener, ShouldDieMirrorContext context)
    {
        return Registry.Invoke(listener, context, true).Value;
    }

    public static bool InvokeLate(AbstractModel listener, ShouldDieMirrorContext context)
    {
        return LateRegistry.Invoke(listener, context, true).Value;
    }

    private static Registry CreateRegistry()
    {
        var registry = new Registry(ShouldDie);

        registry.Register<FairyInABottle>(FairyInABottleMirrors.ShouldDie);

        return registry;
    }

    private static Registry CreateLateRegistry()
    {
        var registry = new Registry(ShouldDieLate);

        registry.Register<LizardTail>(LizardTailMirrors.ShouldDieLate);

        return registry;
    }
}

internal sealed class ShouldDieMirrorContext : CombatPredictionMirrorContext
{
    public required Creature Creature { get; init; }
}
