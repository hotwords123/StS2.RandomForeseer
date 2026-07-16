using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.ValueProps;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.Common.Mirrors;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Hooks.Damage;

using Registry = ModelMethodMirrorRegistry<AbstractModel, AfterDamageReceivedMirrorContext>;

// Mirrors the prediction-relevant parts of Hook.AfterDamageReceived and its late phase.
internal static class AfterDamageReceivedMirrors
{
    private static readonly MirrorMethodSpec AfterDamageReceived = MirrorMethodSpec.Hook(
        nameof(AbstractModel.AfterDamageReceived),
        [
            typeof(PlayerChoiceContext),
            typeof(Creature),
            typeof(DamageResult),
            typeof(ValueProp),
            typeof(Creature),
            typeof(CardModel)
        ]);

    private static readonly MirrorMethodSpec AfterDamageReceivedLate = MirrorMethodSpec.Hook(
        nameof(AbstractModel.AfterDamageReceivedLate),
        [
            typeof(PlayerChoiceContext),
            typeof(Creature),
            typeof(DamageResult),
            typeof(ValueProp),
            typeof(Creature),
            typeof(CardModel)
        ]);

    private static readonly Registry Registry = CreateRegistry();
    private static readonly Registry LateRegistry = new(AfterDamageReceivedLate);

    public static void Invoke(AbstractModel listener, AfterDamageReceivedMirrorContext context)
    {
        Registry.Invoke(listener, context);
    }

    public static void InvokeLate(AbstractModel listener, AfterDamageReceivedMirrorContext context)
    {
        LateRegistry.Invoke(listener, context);
    }

    private static Registry CreateRegistry()
    {
        var registry = new Registry(AfterDamageReceived);

        // Combat predictions are scoped to outcomes that can still affect the current player turn.
        // Models that only mutate later-turn or room-end state are ignored here.
        registry.RegisterIgnored<AsleepPower>();
        registry.Register<BeatingRemnant>(HandleBeatingRemnant);
        registry.Register<CentennialPuzzle>(HandleCentennialPuzzle);
        registry.Register<CurlUpPower>(HandleCurlUpPower);
        registry.Register<DemonTongue>(HandleDemonTongue);
        registry.RegisterIgnored<EmotionChip>();
        registry.Register<FlameBarrierPower>(HandleFlameBarrierPower);
        registry.Register<FlutterPower>(HandleFlutterPower);
        registry.Register<HardenedShellPower>(HandleHardenedShellPower);
        registry.RegisterIgnored<LagavulinMatriarch>();
        registry.RegisterIgnored<LavaLamp>();
        registry.Register<InfernoPower>(HandleInfernoPower);
        registry.Register<PersonalHivePower>(HandlePersonalHivePower);
        registry.RegisterIgnored<PlowPower>();
        registry.Register<ReflectPower>(HandleReflectPower);
        registry.Register<RupturePower>(HandleRupturePower);
        registry.Register<SelfFormingClay>(HandleSelfFormingClay);
        registry.RegisterIgnored<ShriekPower>();
        registry.Register<SlipperyPower>(HandleSlipperyPower);
        registry.RegisterIgnored<SlumberPower>();
        registry.Register<TheGambitPower>(HandleTheGambitPower);

        return registry;
    }

    private static void HandleBeatingRemnant(BeatingRemnant relic, AfterDamageReceivedMirrorContext context)
    {
        if (context.Target == relic.Owner.Creature && context.Result.UnblockedDamage > 0)
        {
            // TODO: Track the amount of unblocked damage taken by the owner this turn.
            context.History.RecordRisk(PredictionRiskReason.MethodMirrorIncomplete);
        }
    }

    private static void HandleCentennialPuzzle(CentennialPuzzle relic, AfterDamageReceivedMirrorContext context)
    {
        if (context.Target != relic.Owner.Creature || context.Result.UnblockedDamage <= 0)
        {
            return;
        }

        var state = context.StateStore.Get(relic, () => new CentennialPuzzlePredictionState(relic));
        if (!state.UsedThisCombat)
        {
            state.UsedThisCombat = true;
            context.Simulator.Draw(relic.Owner, relic.DynamicVars.Cards.IntValue);
        }
    }

    private static void HandleCurlUpPower(CurlUpPower power, AfterDamageReceivedMirrorContext context)
    {
        if (context.Target == power.Owner && context.Props.IsPoweredAttack() && context.Source is not null)
        {
            context.History.RecordRisk(PredictionRiskReason.MethodMirrorIncomplete);
        }
    }

    private static void HandleDemonTongue(DemonTongue relic, AfterDamageReceivedMirrorContext context)
    {
        if (context.CombatState.CurrentSide == relic.Owner.Creature.Side &&
            context.Target == relic.Owner.Creature &&
            context.Result.UnblockedDamage > 0)
        {
            var state = context.StateStore.Get(relic, () => new DemonTonguePredictionState(relic));
            if (!state.TriggeredThisTurn)
            {
                state.TriggeredThisTurn = true;
                context.Simulator.Heal(relic.Owner.Creature, context.Result.UnblockedDamage);
            }
        }
    }

    private static void HandleFlameBarrierPower(FlameBarrierPower power, AfterDamageReceivedMirrorContext context)
    {
        if (context.Target == power.Owner && context.Dealer is not null && context.Props.IsPoweredAttack())
        {
            context.Simulator.Damage(context.Dealer, power.Amount, ValueProp.Unpowered, power.Owner);
        }
    }

    private static void HandleFlutterPower(FlutterPower power, AfterDamageReceivedMirrorContext context)
    {
        if (context.Target == power.Owner &&
            context.Result.UnblockedDamage != 0 &&
            context.Props.IsPoweredAttack())
        {
            context.History.RecordRisk(PredictionRiskReason.MethodMirrorIncomplete);
        }
    }

    private static void HandleHardenedShellPower(HardenedShellPower power, AfterDamageReceivedMirrorContext context)
    {
        if (context.Target == power.Owner && context.Result.UnblockedDamage > 0)
        {
            // TODO: Track the amount of unblocked damage taken by the owner this turn.
            context.History.RecordRisk(PredictionRiskReason.MethodMirrorIncomplete);
        }
    }

    private static void HandleInfernoPower(InfernoPower power, AfterDamageReceivedMirrorContext context)
    {
        if (context.Target == power.Owner &&
            context.Result.UnblockedDamage > 0 &&
            context.CombatState.CurrentSide == power.Owner.Side)
        {
            context.Simulator.Damage(context.State.HittableEnemies, power.Amount, ValueProp.Unpowered, power.Owner);
        }
    }

    private static void HandlePersonalHivePower(PersonalHivePower power, AfterDamageReceivedMirrorContext context)
    {
        if (context.Target == power.Owner && context.Dealer is not null && context.Props.IsPoweredAttack())
        {
            var dealer = context.Dealer;
            if (dealer.Monster is Osty)
            {
                dealer = dealer.PetOwner?.Creature;
            }

            if (dealer is not null)
            {
                context.Simulator.AddToCombat<Dazed>(
                    dealer,
                    PileType.Draw,
                    power.Amount,
                    creator: null,
                    CardPilePosition.Random);
            }
        }
    }

    private static void HandleReflectPower(ReflectPower power, AfterDamageReceivedMirrorContext context)
    {
        if (context.Target == power.Owner &&
            context.Result.BlockedDamage > 0 &&
            context.Props.IsPoweredAttack() &&
            context.Dealer is not null)
        {
            context.Simulator.Damage(context.Dealer, context.Result.BlockedDamage, ValueProp.Unpowered, power.Owner);
        }
    }

    private static void HandleRupturePower(RupturePower power, AfterDamageReceivedMirrorContext context)
    {
        if (context.Target == power.Owner &&
            context.Result.UnblockedDamage > 0 &&
            context.CombatState.CurrentSide == power.Owner.Side)
        {
            context.History.RecordRisk(PredictionRiskReason.MethodMirrorIncomplete);
        }
    }

    private static void HandleSelfFormingClay(SelfFormingClay relic, AfterDamageReceivedMirrorContext context)
    {
        if (context.Target == relic.Owner.Creature && context.Result.UnblockedDamage > 0)
        {
            // Vanilla applies next-turn block here, outside the current prediction scope.
        }
    }

    private static void HandleSlipperyPower(SlipperyPower power, AfterDamageReceivedMirrorContext context)
    {
        if (context.Target == power.Owner && context.Result.UnblockedDamage >= 1)
        {
            // TODO: Mirror the power amount decrement in prediction state.
            context.History.RecordRisk(PredictionRiskReason.MethodMirrorIncomplete);
        }
    }

    private static void HandleTheGambitPower(TheGambitPower power, AfterDamageReceivedMirrorContext context)
    {
        if (context.Target == power.Owner &&
            context.Props.IsPoweredAttack() &&
            context.Result.UnblockedDamage > 0)
        {
            // TODO: Vanilla removes the power here.
            context.History.RecordRisk(PredictionRiskReason.MethodMirrorIncomplete);
            context.Simulator.Kill(power.Owner);
        }
    }
}

internal sealed class AfterDamageReceivedMirrorContext : CombatPredictionMirrorContext
{
    public required Creature Target { get; init; }

    public required DamageResult Result { get; init; }

    public required ValueProp Props { get; init; }

    public required Creature? Dealer { get; init; }

    public required PredictedCard? Source { get; init; }
}

internal sealed class CentennialPuzzlePredictionState(CentennialPuzzle relic)
{
    public bool UsedThisCombat { get; set; } = relic.UsedThisCombat;
}

internal sealed class DemonTonguePredictionState(DemonTongue relic)
{
    public bool TriggeredThisTurn { get; set; } = relic._triggeredThisTurn;
}
