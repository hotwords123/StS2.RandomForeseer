using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using RandomForeseer.Common;
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

    public static void Run(ShouldDrawHookContext context)
    {
        Registry.Run(
            context.CombatState.IterateHookListeners(),
            context,
            static context => !context.IsBlocked);
    }

    private static HookRegistry<ShouldDrawHookContext> CreateRegistry()
    {
        var registry = new HookRegistry<ShouldDrawHookContext>(ShouldDraw);

        registry.Register<NoDrawPower>(HandleNoDrawPower);
        registry.Register<Fiddle>(CallOriginal);

        return registry;
    }

    private static void HandleNoDrawPower(NoDrawPower power, ShouldDrawHookContext context)
    {
        if (context.FromHandDraw || context.Player != power.Owner?.Player)
        {
            return;
        }

        // NoDrawPower.ShouldDraw flashes as a side effect, so mirror the predicate instead of calling it.
        context.Block();
    }

    private static void CallOriginal(AbstractModel model, ShouldDrawHookContext context)
    {
        if (!model.ShouldDraw(context.Player, context.FromHandDraw))
        {
            context.Block();
        }
    }
}

internal sealed class ShouldDrawHookContext : IPredictionHookContext
{
    public required PredictionRiskTracker RiskTracker { get; init; }

    public required ICombatState CombatState { get; init; }

    public required Player Player { get; init; }

    public required bool FromHandDraw { get; init; }

    public bool IsBlocked { get; private set; }

    public void Block()
    {
        IsBlocked = true;
    }
}
