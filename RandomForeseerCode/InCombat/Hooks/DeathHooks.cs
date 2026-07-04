using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using RandomForeseer.RandomForeseerCode.Common.Hooks;

namespace RandomForeseer.RandomForeseerCode.InCombat.Hooks;

internal static class DeathHooks
{
    private static readonly HookSpec BeforeDeath = new(
        nameof(AbstractModel.BeforeDeath),
        [typeof(Creature)]);

    private static readonly HookSpec AfterDeath = new(
        nameof(AbstractModel.AfterDeath),
        [
            typeof(PlayerChoiceContext),
            typeof(Creature),
            typeof(bool),
            typeof(float)
        ]);

    private static readonly HookRegistry<BeforeDeathHookContext> BeforeDeathRegistry = CreateBeforeDeathRegistry();
    private static readonly HookRegistry<AfterDeathHookContext> AfterDeathRegistry = CreateAfterDeathRegistry();

    public static void RunBeforeDeath(BeforeDeathHookContext context)
    {
        BeforeDeathRegistry.Run(context.RunState.IterateHookListeners(context.CombatState), context);
    }

    public static void RunAfterDeath(AfterDeathHookContext context)
    {
        AfterDeathRegistry.Run(context.RunState.IterateHookListeners(context.CombatState), context);
    }

    private static HookRegistry<BeforeDeathHookContext> CreateBeforeDeathRegistry()
    {
        var registry = new HookRegistry<BeforeDeathHookContext>(BeforeDeath);

        registry.RegisterIgnored<Crusher>();
        registry.RegisterIgnored<Rocket>();
        registry.RegisterIgnored<HeistPower>();
        registry.RegisterIgnored<SwipePower>();

        return registry;
    }

    private static HookRegistry<AfterDeathHookContext> CreateAfterDeathRegistry()
    {
        var registry = new HookRegistry<AfterDeathHookContext>(AfterDeath);

        registry.RegisterIgnored<Aeonglass>();
        registry.RegisterIgnored<DecimillipedeSegment>();
        registry.RegisterIgnored<KinPriest>();
        registry.RegisterIgnored<LagavulinMatriarch>();
        registry.RegisterIgnored<Queen>();
        registry.RegisterIgnored<SoulFysh>();
        registry.RegisterIgnored<TestSubject>();
        registry.RegisterIgnored<TheInsatiable>();
        registry.RegisterIgnored<Vantom>();
        registry.RegisterIgnored<WaterfallGiant>();

        registry.Register<GremlinHorn>(HandleGremlinHorn);
        registry.Register<Melancholy>(HandleMelancholy);

        return registry;
    }

    private static void HandleGremlinHorn(GremlinHorn relic, AfterDeathHookContext context)
    {
        if (context.Creature.Side == relic.Owner.Creature.Side)
        {
            return;
        }

        // Vanilla also gains energy here; energy is outside the current prediction surface.
        context.Simulator.Draw(relic.Owner, relic.DynamicVars.Cards.IntValue);
    }

    private static void HandleMelancholy(Melancholy card, AfterDeathHookContext context)
    {
        if (context.WasRemovalPrevented)
        {
            return;
        }

        var predictedCard = context.State.FindCard(card);
        predictedCard?.MutablePreview.EnergyCost.AddThisCombat(-card.DynamicVars.Energy.IntValue);
    }
}

internal sealed class BeforeDeathHookContext : CombatPredictionHookContext
{
    public required Creature Creature { get; init; }
}

internal sealed class AfterDeathHookContext : CombatPredictionHookContext
{
    public required Creature Creature { get; init; }

    public bool WasRemovalPrevented { get; init; }
}
