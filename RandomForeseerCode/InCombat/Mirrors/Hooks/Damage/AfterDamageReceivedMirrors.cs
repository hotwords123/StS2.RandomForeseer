using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.ValueProps;
using RandomForeseer.Common;
using RandomForeseer.Common.Mirrors;
using RandomForeseer.InCombat.Mirrors.Hooks.Card;

namespace RandomForeseer.InCombat.Mirrors.Hooks.Damage;

using Registry = MethodMirrorRegistry<AbstractModel, AfterDamageReceivedMirrorContext>;

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
        if (context.Target == relic.Owner.Creature)
        {
            var state = context.StateStore.Get(relic, () => new BeatingRemnantPredictionState(relic));
            state.DamageReceivedThisTurn += context.Result.UnblockedDamage;
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
            context.Simulator.Draw(relic.Owner, relic.DynamicVars.Cards.BaseValue);
        }
    }

    private static void HandleCurlUpPower(CurlUpPower power, AfterDamageReceivedMirrorContext context)
    {
        if (context.Target != power.Owner || !context.Props.IsPoweredAttack() || context.Source is null)
        {
            return;
        }

        var state = context.StateStore.Get<CurlUpPredictionState>(power);
        if (state is { Consumed: false, PlayedCard: null })
        {
            state.PlayedCard = context.Source.Original;
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
            context.StateStore.GetPowerAmount(power).Decrement();
        }
    }

    private static void HandleHardenedShellPower(HardenedShellPower power, AfterDamageReceivedMirrorContext context)
    {
        if (context.Target == power.Owner && !context.Result.WasFullyBlocked)
        {
            var state = context.StateStore.Get(power, () => new HardenedShellPredictionState(power));
            state.DamageReceivedThisTurn += context.Result.UnblockedDamage;

            if (state.DamageReceivedThisTurn >= power.Amount)
            {
                context.State.GetCreature(power.Owner).HpDisplay = HpDisplay.InfiniteWithNumbers;
            }
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

            if (dealer?.Player is { } player)
            {
                context.Simulator.CreateAndAddGeneratedCardsToCombat<Dazed>(
                    player,
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
            context.StateStore.GetPowerAmount(power).Decrement();
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

internal sealed class AfterDamageReceivedMirrorContext : CombatMirrorContext
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

internal sealed class BeatingRemnantPredictionState(BeatingRemnant relic)
{
    public decimal DamageReceivedThisTurn { get; set; } = relic._damageReceivedThisTurn;
}

internal sealed class HardenedShellPredictionState(HardenedShellPower power)
{
    public decimal DamageReceivedThisTurn { get; set; } =
        power.GetInternalData<HardenedShellPower.Data>().damageReceivedThisTurn;
}
