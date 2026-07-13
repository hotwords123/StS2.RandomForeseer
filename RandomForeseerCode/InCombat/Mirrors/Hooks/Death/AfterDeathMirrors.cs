using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Relics;
using RandomForeseer.RandomForeseerCode.Common.Mirrors;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Hooks.Death;

using Registry = ModelMethodMirrorRegistry<AbstractModel, AfterDeathMirrorContext>;

// Mirrors the prediction-relevant parts of Hook.AfterDeath.
internal static class AfterDeathMirrors
{
    private static readonly MirrorMethodSpec AfterDeath = MirrorMethodSpec.Hook(
        nameof(AbstractModel.AfterDeath),
        [
            typeof(PlayerChoiceContext),
            typeof(Creature),
            typeof(bool),
            typeof(float)
        ]);

    private static readonly Registry Registry = CreateRegistry();

    public static void Invoke(AbstractModel listener, AfterDeathMirrorContext context)
    {
        Registry.Invoke(listener, context);
    }

    private static Registry CreateRegistry()
    {
        var registry = new Registry(AfterDeath);

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

    private static void HandleGremlinHorn(GremlinHorn relic, AfterDeathMirrorContext context)
    {
        if (context.Creature.Side != relic.Owner.Creature.Side)
        {
            context.Simulator.GainEnergy(relic.Owner, relic.DynamicVars.Energy.BaseValue);
            context.Simulator.Draw(relic.Owner, relic.DynamicVars.Cards.IntValue);
        }
    }

    private static void HandleMelancholy(Melancholy card, AfterDeathMirrorContext context)
    {
        if (!context.WasRemovalPrevented)
        {
            var previewCard = context.State.FindCard(card)?.MutablePreview;
            previewCard?.EnergyCost.AddThisCombat(-previewCard.DynamicVars.Energy.IntValue);
        }
    }
}

internal sealed class AfterDeathMirrorContext : CombatPredictionMirrorContext
{
    public required Creature Creature { get; init; }

    public required bool WasRemovalPrevented { get; init; }
}
