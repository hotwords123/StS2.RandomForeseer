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

    public static void Run(ShouldDrawHookContext context)
    {
        Registry.Run(context.CombatState.IterateHookListeners(), context);
    }

    private static HookRegistry<ShouldDrawHookContext> CreateRegistry()
    {
        var registry = new HookRegistry<ShouldDrawHookContext>(ShouldDraw);

        registry.Register<NoDrawPower>(HandleNoDrawPower);
        registry.Register<Fiddle>(HandleFiddle);

        return registry;
    }

    private static void HandleNoDrawPower(NoDrawPower power, ShouldDrawHookContext context)
    {
        if (!context.FromHandDraw && context.Player == power.Owner?.Player)
        {
            context.Block();
        }
    }

    private static void HandleFiddle(Fiddle relic, ShouldDrawHookContext context)
    {
        if (!context.FromHandDraw &&
            context.Player == relic.Owner &&
            context.Player.Creature.Side == context.CombatState.CurrentSide)
        {
            context.Block();
        }
    }
}

internal sealed class ShouldDrawHookContext : CombatPredictionHookContext
{
    public required Player Player { get; init; }

    public required bool FromHandDraw { get; init; }

    public bool IsBlocked { get; private set; }

    public override bool ShouldContinue => !IsBlocked;

    public void Block()
    {
        IsBlocked = true;
    }
}
