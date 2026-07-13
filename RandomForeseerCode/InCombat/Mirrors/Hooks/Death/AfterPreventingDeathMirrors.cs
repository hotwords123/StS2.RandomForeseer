using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Models.Relics;
using RandomForeseer.RandomForeseerCode.Common.Mirrors;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Hooks.Death;

using Registry = ModelMethodMirrorRegistry<AbstractModel, AfterPreventingDeathMirrorContext>;

// Mirrors the prediction-relevant parts of Hook.AfterPreventingDeath.
internal static class AfterPreventingDeathMirrors
{
    private static readonly MirrorMethodSpec AfterPreventingDeath = MirrorMethodSpec.Hook(
        nameof(AbstractModel.AfterPreventingDeath),
        [typeof(Creature)]);

    private static readonly Registry Registry = CreateRegistry();

    public static void Invoke(AbstractModel preventer, AfterPreventingDeathMirrorContext context)
    {
        Registry.Invoke(preventer, context);
    }

    private static Registry CreateRegistry()
    {
        var registry = new Registry(AfterPreventingDeath);

        registry.Register<FairyInABottle>(FairyInABottleMirrors.AfterPreventingDeath);
        registry.Register<LizardTail>(LizardTailMirrors.AfterPreventingDeath);

        return registry;
    }
}

internal sealed class AfterPreventingDeathMirrorContext : CombatPredictionMirrorContext
{
    public required Creature Creature { get; init; }
}
