using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using RandomForeseer.Common;
using RandomForeseer.Common.Mirrors;

namespace RandomForeseer.InCombat.Mirrors.Hooks.Death;

using Registry = MethodMirrorRegistry<AbstractModel, AfterDeathMirrorContext>;

// Mirrors the prediction-relevant parts of Hook.AfterDeath.
internal static class AfterDeathMirrors
{
    private static readonly MirrorMethodSpec AfterDeath = MirrorMethodSpec.Hook(
        nameof(AbstractModel.AfterDeath),
        [
            typeof(PlayerChoiceContext),
            typeof(Creature),
            typeof(CardModel),
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

        registry.Register<CrabRagePower>(HandleCrabRagePower);
        registry.Register<CompostPower>(HandleCompostPower);
        registry.Register<GremlinHorn>(HandleGremlinHorn);
        registry.Register<Melancholy>(HandleMelancholy);

        return registry;
    }

    private static void HandleCrabRagePower(CrabRagePower power, AfterDeathMirrorContext context)
    {
        if (context.Creature == power.Owner || context.Creature.Side != power.Owner.Side)
        {
            return;
        }

        var state = context.StateStore.GetPowerAmount(power);
        if (!state.IsActive)
        {
            return;
        }

        // Strength application and power removal callbacks/listener membership remain unmodeled.
        // Vanilla does not check WasRemovalPrevented before granting block.
        context.History.RecordRisk(PredictionRiskReason.MethodMirrorIncomplete);
        context.Simulator.GainBlock(power.Owner, power.DynamicVars.Block);
        state.Consume();
    }

    private static void HandleGremlinHorn(GremlinHorn relic, AfterDeathMirrorContext context)
    {
        if (context.Creature.Side != relic.Owner.Creature.Side)
        {
            context.Simulator.GainEnergy(relic.Owner, relic.DynamicVars.Energy.BaseValue);
            context.Simulator.Draw(relic.Owner, relic.DynamicVars.Cards.BaseValue);
        }
    }

    private static void HandleCompostPower(CompostPower power, AfterDeathMirrorContext context)
    {
        if (context.Creature.Side == power.Owner.Side)
        {
            return;
        }

        foreach (var teammate in context.State.GetTeammatesOf(power.Owner))
        {
            if (teammate.Player is { } player && context.State.GetCreature(teammate).IsAlive)
            {
                context.Simulator.GainEnergy(player, power.Amount);
            }
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

internal sealed class AfterDeathMirrorContext : CombatMirrorContext
{
    public required Creature Creature { get; init; }

    public PredictedCard? Source { get; init; }

    public required bool WasRemovalPrevented { get; init; }
}
