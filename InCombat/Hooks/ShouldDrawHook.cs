using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using RandomForeseer.Common.Hooks;

namespace RandomForeseer.InCombat.Hooks;

// Mirrors the prediction-relevant parts of Hook.ShouldDraw.
internal static class ShouldDrawHook
{
    private static readonly HookSpec ShouldDraw = new(
        nameof(AbstractModel.ShouldDraw),
        [
            typeof(Player),
            typeof(bool)
        ]);

    private static readonly HookRegistry<ShouldDrawHookContext> Registry = CreateRegistry();

    public static IReadOnlyList<HookResult> Run(ShouldDrawHookContext context)
    {
        return Registry.Run(context.CombatState.IterateHookListeners(), context);
    }

    private static HookRegistry<ShouldDrawHookContext> CreateRegistry()
    {
        var registry = new HookRegistry<ShouldDrawHookContext>(ShouldDraw, "Draw-pile prediction");

        registry.Register<NoDrawPower>(HandleNoDrawPower);
        registry.Register<Fiddle>(CallOriginal);

        return registry;
    }

    private static HookResultKind HandleNoDrawPower(NoDrawPower power, ShouldDrawHookContext context)
    {
        if (context.FromHandDraw || context.Player != power.Owner?.Player)
        {
            return HookResultKind.Ignored;
        }

        // NoDrawPower.ShouldDraw flashes as a side effect, so mirror the predicate instead of calling it.
        return HookResultKind.Blocked;
    }

    private static HookResultKind CallOriginal(AbstractModel model, ShouldDrawHookContext context)
    {
        return model.ShouldDraw(context.Player, context.FromHandDraw)
            ? HookResultKind.Ignored
            : HookResultKind.Blocked;
    }
}

internal sealed class ShouldDrawHookContext
{
    public required ICombatState CombatState { get; init; }

    public required Player Player { get; init; }

    public required bool FromHandDraw { get; init; }
}
