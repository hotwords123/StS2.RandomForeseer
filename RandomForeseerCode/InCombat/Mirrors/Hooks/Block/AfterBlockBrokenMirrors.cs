using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.Common.Mirrors;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Hooks.Block;

using Registry = ModelMethodMirrorRegistry<AbstractModel, AfterBlockBrokenMirrorContext>;

// Mirrors the prediction-relevant parts of Hook.AfterBlockBroken.
internal static class AfterBlockBrokenMirrors
{
    private static readonly MirrorMethodSpec AfterBlockBroken = MirrorMethodSpec.Hook(
        nameof(AbstractModel.AfterBlockBroken),
        [typeof(PlayerChoiceContext), typeof(Creature), typeof(Creature)]);

    private static readonly Registry Registry = CreateRegistry();

    public static void Invoke(AbstractModel listener, AfterBlockBrokenMirrorContext context)
    {
        Registry.Invoke(listener, context);
    }

    private static Registry CreateRegistry()
    {
        var registry = new Registry(AfterBlockBroken);

        registry.RegisterIgnored<BurrowedPower>();
        registry.Register<HandDrill>(HandleHandDrill);

        return registry;
    }

    private static void HandleHandDrill(HandDrill relic, AfterBlockBrokenMirrorContext context)
    {
        if ((context.Breaker == relic.Owner.Creature || context.Breaker?.PetOwner == relic.Owner) &&
            !context.Target.IsPlayer)
        {
            context.History.RecordRisk(PredictionRiskReason.MethodMirrorIncomplete);
        }
    }
}

internal sealed class AfterBlockBrokenMirrorContext : CombatPredictionMirrorContext
{
    public required Creature Target { get; init; }

    public required Creature? Breaker { get; init; }
}
