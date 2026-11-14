using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Afflictions;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.ValueProps;
using RandomForeseer.Common.Mirrors;
using RandomForeseer.InCombat.Mirrors.Hooks.Card;
using RandomForeseer.InCombat.Mirrors.Shared;
using RandomForeseer.InCombat.Simulation;

namespace RandomForeseer.InCombat.Mirrors.Hooks.TurnEnd;

using Registry = MethodMirrorRegistry<AbstractModel, SideTurnEndMirrorContext>;

/// <summary>
/// Mirrors <see cref="AbstractModel.AfterSideTurnEnd"/> and <see cref="AbstractModel.AfterSideTurnEndLate"/>.
/// </summary>
internal static class AfterSideTurnEndMirrors
{
    // Full power removal/duration mirrors are not available yet. Unregistered side-end callbacks
    // intentionally add no risk; uncommon chained effects may differ (see docs/hooks/end-turn-hooks.md).
    private static readonly MirrorMethodSpec AfterSideTurnEnd = MirrorMethodSpec.Hook(
        nameof(AbstractModel.AfterSideTurnEnd),
        [typeof(PlayerChoiceContext), typeof(CombatSide), typeof(IEnumerable<Creature>)]);

    private static readonly MirrorMethodSpec AfterSideTurnEndLate = MirrorMethodSpec.Hook(
        nameof(AbstractModel.AfterSideTurnEndLate),
        [typeof(PlayerChoiceContext), typeof(CombatSide), typeof(IEnumerable<Creature>)]);

    private static readonly Registry Registry = CreateRegistry();
    private static readonly Registry LateRegistry = CreateLateRegistry();

    public static void Invoke(AbstractModel listener, SideTurnEndMirrorContext context)
    {
        Registry.TryInvokeRegistered(listener, context, out _);
    }

    public static void InvokeLate(AbstractModel listener, SideTurnEndMirrorContext context)
    {
        LateRegistry.TryInvokeRegistered(listener, context, out _);
    }

    private static Registry CreateRegistry()
    {
        var registry = new Registry(AfterSideTurnEnd);

        registry.Register<BurstPower>(ConsumePowerIfParticipant);
        registry.Register<ConstrictPower>(HandleConstrictPower);
        registry.Register<ConsumingShadowPower>(HandleConsumingShadowPower);
        registry.Register<DarkEmbracePower>(HandleDarkEmbracePower);
        registry.Register<DemisePower>(HandleDemisePower);
        registry.Register<DoomPower>(DoomPowerMirrors.AfterSideTurnEnd);
        registry.Register<DoubleDamagePower>(DecrementPowerIfParticipant);
        registry.Register<DuplicationPower>(ConsumePowerIfParticipant);
        registry.Register<EncorePower>(HandleEncorePower);
        registry.Register<HellraiserPower>(HandleHellraiserPower);
        registry.Register<JugglingPower>(HandleJugglingPower);
        registry.Register<KiVersePower>(ConsumePowerIfParticipant);
        registry.Register<MagicBombPower>(HandleMagicBombPower);
        registry.Register<NoDrawPower>(ConsumePowerIfParticipant);
        registry.Register<OneTwoPunchPower>(ConsumePowerIfParticipant);
        registry.Register<PanachePower>(HandlePanachePower);
        registry.Register<ShrinkPower>(HandleShrinkPower);
        registry.Register<SmoggyPower>(HandleSmoggyPower);
        registry.Register<JossPaper>(JossPaperMirrors.AfterSideTurnEnd);

        registry.Register<Kusarigama>(HandleKusarigama);
        registry.Register<ParryingShield>(HandleParryingShield);

        return registry;
    }

    private static Registry CreateLateRegistry()
    {
        var registry = new Registry(AfterSideTurnEndLate);

        registry.Register<DisintegrationPower>(HandleDisintegrationPower);
        registry.Register<InsatiableDominionPower>(HandleInsatiableDominionPower);

        return registry;
    }

    private static void HandleConstrictPower(ConstrictPower power, SideTurnEndMirrorContext context)
    {
        if (context.Participants.Contains(power.Owner) && context.StateStore.GetPowerAmount(power).IsActive)
        {
            context.Simulator.Damage(
                power.Owner,
                power.Amount,
                ValueProp.Unpowered,
                power.Owner);
        }
    }

    private static void HandleConsumingShadowPower(ConsumingShadowPower power, SideTurnEndMirrorContext context)
    {
        if (!context.Participants.Contains(power.Owner) ||
            power.Owner.Player is not { } player ||
            context.State.GetPlayerCombatState(player).OrbQueue.Orbs.Count == 0)
        {
            return;
        }

        for (var i = 0; i < power.Amount; i++)
        {
            context.Simulator.OrbEvokeLast(player);
        }
    }

    private static void HandleDarkEmbracePower(DarkEmbracePower power, SideTurnEndMirrorContext context)
    {
        if (!context.Participants.Contains(power.Owner) || power.Owner.Player is not { } player)
        {
            return;
        }

        var state = context.StateStore.Get(power, () => new DarkEmbracePredictionState(power));
        context.Simulator.Draw(player, power.Amount * state.EtherealCount);
        state.EtherealCount = 0;
    }

    private static void HandleDemisePower(DemisePower power, SideTurnEndMirrorContext context)
    {
        if (context.Participants.Contains(power.Owner))
        {
            context.Simulator.Damage(power.Owner, power.Amount, DamageProps.nonCardHpLoss, dealer: null);
        }
    }

    private static void HandleDisintegrationPower(DisintegrationPower power, SideTurnEndMirrorContext context)
    {
        if (context.Participants.Contains(power.Owner))
        {
            context.Simulator.Damage(power.Owner, power.Amount, ValueProp.Unpowered, power.Owner);
        }
    }

    private static void HandleEncorePower(EncorePower power, SideTurnEndMirrorContext context)
    {
        if (context.Participants.Contains(power.Owner))
        {
            context.StateStore.Get(power, () => new EncorePredictionState(power)).Removed = true;
        }
    }

    private static void HandleHellraiserPower(HellraiserPower power, SideTurnEndMirrorContext context)
    {
        if (context.Participants.Contains(power.Owner))
        {
            var state = context.StateStore.Get(power, () => new HellraiserPredictionState(power));
            state.InfiniteAutoPlaysThisTurn = 0;
        }
    }

    private static void HandleInsatiableDominionPower(InsatiableDominionPower power, SideTurnEndMirrorContext context)
    {
        if (context.Participants.Contains(power.Owner))
        {
            context.Simulator.Damage(power.Owner, power.DynamicVars.Damage, dealer: null);
        }
    }

    private static void HandleJugglingPower(JugglingPower power, SideTurnEndMirrorContext context)
    {
        if (context.Participants.Contains(power.Owner))
        {
            var state = context.StateStore.Get(power, () => new JugglingPredictionState(power));
            state.AttacksPlayedThisTurn = 0;
        }
    }

    private static void HandleMagicBombPower(MagicBombPower power, SideTurnEndMirrorContext context)
    {
        var state = context.StateStore.GetPowerAmount(power);
        if (!context.Participants.Contains(power.Owner) ||
            !state.IsActive ||
            power.Applier is not { } applier ||
            !context.State.GetCreature(applier).IsAlive)
        {
            return;
        }

        context.Simulator.Damage(power.Owner, state.Amount, ValueProp.Unpowered, power.Owner);
        state.Consume();
    }

    private static void HandlePanachePower(PanachePower power, SideTurnEndMirrorContext context)
    {
        if (context.Participants.Contains(power.Owner))
        {
            var state = context.StateStore.Get(power, () => new PanachePredictionState(power));
            state.CardsLeft = PanachePower._baseCardsLeft;
        }
    }

    private static void HandleShrinkPower(ShrinkPower power, SideTurnEndMirrorContext context)
    {
        if (context.StateStore.GetPowerAmount(power).Amount >= 0)
        {
            DecrementPowerIfParticipant(power, context);
        }
    }

    private static void HandleSmoggyPower(SmoggyPower power, SideTurnEndMirrorContext context)
    {
        if (!context.Participants.Contains(power.Owner) || power.Owner.Player is not { } player)
        {
            return;
        }

        foreach (var card in context.State.GetPlayerCombatState(player).AllCards.ToArray())
        {
            if (card.Preview.Affliction is Smog)
            {
                card.ClearAffliction();
            }
        }
    }

    private static void HandleKusarigama(Kusarigama relic, SideTurnEndMirrorContext context)
    {
        if (context.Participants.Contains(relic.Owner.Creature))
        {
            var state = context.StateStore.Get(relic, () => new CounterPredictionState(relic._attacksPlayedThisTurn));
            state.Value = 0;
        }
    }

    private static void HandleParryingShield(ParryingShield relic, SideTurnEndMirrorContext context)
    {
        if (!context.Participants.Contains(relic.Owner.Creature) ||
            context.State.GetCreature(relic.Owner.Creature).Block < relic.DynamicVars.Block.BaseValue)
        {
            return;
        }

        var target = context.Rng.CombatTargets.NextItem(context.State.HittableEnemies);
        if (target is not null)
        {
            context.Simulator.Damage(target, relic.DynamicVars.Damage, relic.Owner.Creature);
        }
    }

    private static void DecrementPowerIfParticipant(PowerModel power, SideTurnEndMirrorContext context)
    {
        if (context.Participants.Contains(power.Owner))
        {
            context.StateStore.GetPowerAmount(power).Decrement();
        }
    }

    private static void ConsumePowerIfParticipant(PowerModel power, SideTurnEndMirrorContext context)
    {
        if (context.Participants.Contains(power.Owner))
        {
            context.StateStore.GetPowerAmount(power).Consume();
        }
    }
}
