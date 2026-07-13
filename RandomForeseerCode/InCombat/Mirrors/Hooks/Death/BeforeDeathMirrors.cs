using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using RandomForeseer.RandomForeseerCode.Common.Mirrors;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Hooks.Death;

using Registry = ModelMethodMirrorRegistry<AbstractModel, BeforeDeathMirrorContext>;

// Mirrors the prediction-relevant parts of Hook.BeforeDeath.
internal static class BeforeDeathMirrors
{
    private static readonly MirrorMethodSpec BeforeDeath = MirrorMethodSpec.Hook(
        nameof(AbstractModel.BeforeDeath),
        [typeof(Creature)]);

    private static readonly Registry Registry = CreateRegistry();

    public static void Invoke(AbstractModel listener, BeforeDeathMirrorContext context)
    {
        Registry.Invoke(listener, context);
    }

    private static Registry CreateRegistry()
    {
        var registry = new Registry(BeforeDeath);

        registry.RegisterIgnored<Crusher>();
        registry.RegisterIgnored<Rocket>();
        registry.RegisterIgnored<HeistPower>();
        registry.RegisterIgnored<SwipePower>();

        return registry;
    }
}

internal sealed class BeforeDeathMirrorContext : CombatPredictionMirrorContext
{
    public required Creature Creature { get; init; }
}
