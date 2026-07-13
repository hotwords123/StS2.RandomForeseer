using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using RandomForeseer.RandomForeseerCode.Common.Mirrors;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Hooks.Card;

using Registry = ModelMethodMirrorRegistry<AbstractModel, ShouldDrawMirrorContext, bool>;

// Mirrors the prediction-relevant parts of Hook.ShouldDraw.
internal static class ShouldDrawMirrors
{
    private static readonly MirrorMethodSpec ShouldDraw = MirrorMethodSpec.Hook(
        nameof(AbstractModel.ShouldDraw),
        [typeof(Player), typeof(bool)]);

    private static readonly Registry Registry = CreateRegistry();

    public static bool Invoke(AbstractModel listener, ShouldDrawMirrorContext context)
    {
        return Registry.Invoke(listener, context, true).Value;
    }

    private static Registry CreateRegistry()
    {
        var registry = new Registry(ShouldDraw);

        registry.Register<NoDrawPower>(HandleNoDrawPower);
        registry.Register<Fiddle>(HandleFiddle);

        return registry;
    }

    private static bool HandleNoDrawPower(NoDrawPower power, ShouldDrawMirrorContext context)
    {
        if (!context.FromHandDraw && context.Player == power.Owner?.Player)
        {
            return false;
        }

        return true;
    }

    private static bool HandleFiddle(Fiddle relic, ShouldDrawMirrorContext context)
    {
        if (!context.FromHandDraw &&
            context.Player == relic.Owner &&
            context.CombatState.CurrentSide == context.Player.Creature.Side)
        {
            return false;
        }

        return true;
    }
}

internal sealed class ShouldDrawMirrorContext : CombatPredictionMirrorContext
{
    public required Player Player { get; init; }

    public required bool FromHandDraw { get; init; }
}
