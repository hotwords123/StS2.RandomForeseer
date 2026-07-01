using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.ValueProps;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.Common.Hooks;

namespace RandomForeseer.RandomForeseerCode.InCombat.Hooks;

internal static class DamageReceivedHooks
{
    private static readonly HookSpec BeforeDamageReceived = new(
        nameof(AbstractModel.BeforeDamageReceived),
        [
            typeof(PlayerChoiceContext),
            typeof(Creature),
            typeof(decimal),
            typeof(ValueProp),
            typeof(Creature),
            typeof(CardModel)
        ]);

    private static readonly HookSpec AfterDamageReceived = new(
        nameof(AbstractModel.AfterDamageReceived),
        [
            typeof(PlayerChoiceContext),
            typeof(Creature),
            typeof(DamageResult),
            typeof(ValueProp),
            typeof(Creature),
            typeof(CardModel)
        ]);

    private static readonly HookSpec AfterDamageReceivedLate = new(
        nameof(AbstractModel.AfterDamageReceivedLate),
        [
            typeof(PlayerChoiceContext),
            typeof(Creature),
            typeof(DamageResult),
            typeof(ValueProp),
            typeof(Creature),
            typeof(CardModel)
        ]);

    private static readonly HookRegistry<BeforeDamageReceivedHookContext> BeforeRegistry = CreateBefore();
    private static readonly HookRegistry<AfterDamageReceivedHookContext> AfterRegistry = CreateAfter();
    private static readonly HookRegistry<AfterDamageReceivedHookContext> AfterLateRegistry = new(AfterDamageReceivedLate);

    public static void RunBefore(BeforeDamageReceivedHookContext context)
    {
        BeforeRegistry.Run(context.RunState.IterateHookListeners(context.CombatState), context);
    }

    public static void RunAfter(AfterDamageReceivedHookContext context)
    {
        AfterRegistry.Run(context.RunState.IterateHookListeners(context.CombatState), context);
    }

    public static void RunAfterLate(AfterDamageReceivedHookContext context)
    {
        AfterLateRegistry.Run(context.RunState.IterateHookListeners(context.CombatState), context);
    }

    private static HookRegistry<BeforeDamageReceivedHookContext> CreateBefore()
    {
        var registry = new HookRegistry<BeforeDamageReceivedHookContext>(BeforeDamageReceived);

        registry.Register<ThornsPower>(HandleThornsPower);

        return registry;
    }

    private static HookRegistry<AfterDamageReceivedHookContext> CreateAfter()
    {
        var registry = new HookRegistry<AfterDamageReceivedHookContext>(AfterDamageReceived);

        // Currently, combat predictions are scoped to outcomes that can still affect the current player turn.
        // Models that would mutate state for an enemy turn, a later player turn, or room-end rewards are ignored for now.

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

    private static void HandleThornsPower(ThornsPower power, BeforeDamageReceivedHookContext context)
    {
        if (context.Target == power.Owner &&
            context.Dealer != null &&
            (context.Props.IsPoweredAttack() || context.Source?.Original is Omnislice))
        {
            context.Simulator.Damage(
                context.Dealer,
                power.Amount,
                ValueProp.Unpowered | ValueProp.SkipHurtAnim,
                power.Owner);
        }
    }

    private static void HandleBeatingRemnant(BeatingRemnant relic, AfterDamageReceivedHookContext context)
    {
        if (context.Target == relic.Owner.Creature && context.Result.UnblockedDamage > 0)
        {
            // TODO: Track the amount of unblocked damage taken by the owner this turn.
            context.MarkCurrentSourceRisky();
        }
    }

    private static void HandleCentennialPuzzle(CentennialPuzzle relic, AfterDamageReceivedHookContext context)
    {
        if (context.Target != relic.Owner.Creature || context.Result.UnblockedDamage <= 0)
        {
            return;
        }

        var state = context.StateStore.Get(relic, () => new CentennialPuzzlePredictionState
        {
            UsedThisCombat = relic.UsedThisCombat
        });
        if (!state.UsedThisCombat)
        {
            state.UsedThisCombat = true;
            context.Simulator.Draw(relic.Owner, relic.DynamicVars.Cards.IntValue);
        }
    }

    private static void HandleCurlUpPower(CurlUpPower power, AfterDamageReceivedHookContext context)
    {
        if (context.Target == power.Owner && context.Props.IsPoweredAttack() && context.Source != null)
        {
            context.MarkCurrentSourceRisky();
        }
    }

    private static void HandleDemonTongue(DemonTongue relic, AfterDamageReceivedHookContext context)
    {
        if (context.CombatState.CurrentSide == relic.Owner.Creature.Side &&
            context.Target == relic.Owner.Creature &&
            context.Result.UnblockedDamage > 0)
        {
            context.MarkCurrentSourceRisky();
        }
    }

    private static void HandleFlameBarrierPower(FlameBarrierPower power, AfterDamageReceivedHookContext context)
    {
        if (context.Target == power.Owner && context.Dealer != null && context.Props.IsPoweredAttack())
        {
            context.Simulator.Damage(context.Dealer, power.Amount, ValueProp.Unpowered, power.Owner);
        }
    }

    private static void HandleFlutterPower(FlutterPower power, AfterDamageReceivedHookContext context)
    {
        if (context.Target == power.Owner &&
            context.Result.UnblockedDamage != 0 &&
            context.Props.IsPoweredAttack())
        {
            context.MarkCurrentSourceRisky();
        }
    }

    private static void HandleHardenedShellPower(HardenedShellPower power, AfterDamageReceivedHookContext context)
    {
        if (context.Target == power.Owner && context.Result.UnblockedDamage > 0)
        {
            // TODO: Track the amount of unblocked damage taken by the owner this turn.
            context.MarkCurrentSourceRisky();
        }
    }

    private static void HandleInfernoPower(InfernoPower power, AfterDamageReceivedHookContext context)
    {
        if (context.Target == power.Owner &&
            context.Result.UnblockedDamage > 0 &&
            context.CombatState.CurrentSide == power.Owner.Side)
        {
            context.Simulator.Damage(
                context.State.GetHittableOpponentsOf(power.Owner),
                power.Amount,
                ValueProp.Unpowered,
                power.Owner);
        }
    }

    private static void HandlePersonalHivePower(PersonalHivePower power, AfterDamageReceivedHookContext context)
    {
        if (context.Target == power.Owner && context.Dealer != null && context.Props.IsPoweredAttack())
        {
            var dealer = context.Dealer is { Monster: Osty } osty
                ? osty.PetOwner?.Creature
                : context.Dealer;

            if (dealer?.Player is { } player)
            {
                for (int i = 0; i < power.Amount; i++)
                {
                    // TODO: Streamline this with the BiiigHug shuffle logic in ShuffleHooks.cs
                    var dazed = PredictionUtils.CreateCard(ModelDb.Card<Dazed>(), player);
                    var drawPileCards = context.State.GetPlayerCombatState(player).DrawPileCards;
                    var position = context.Rng.Shuffle.NextInt(drawPileCards.Count + 1);
                    drawPileCards.Insert(position, PredictedCard.FromGenerated(dazed));
                }
            }
        }
    }

    private static void HandleReflectPower(ReflectPower power, AfterDamageReceivedHookContext context)
    {
        if (context.Target == power.Owner &&
            context.Result.BlockedDamage > 0 &&
            context.Props.IsPoweredAttack() &&
            context.Dealer != null)
        {
            context.Simulator.Damage(context.Dealer, context.Result.BlockedDamage, ValueProp.Unpowered, power.Owner);
        }
    }

    private static void HandleRupturePower(RupturePower power, AfterDamageReceivedHookContext context)
    {
        if (context.Target == power.Owner &&
            context.Result.UnblockedDamage > 0 &&
            context.CombatState.CurrentSide == power.Owner.Side)
        {
            context.MarkCurrentSourceRisky();
        }
    }

    private static void HandleSelfFormingClay(SelfFormingClay relic, AfterDamageReceivedHookContext context)
    {
        if (context.Target == relic.Owner.Creature && context.Result.UnblockedDamage > 0)
        {
            // Vanilla applies SelfFormingClayPower here, which gains block at the start of the next turn.
            // This does not affect the current combat simulation, so ignore it for now.
        }
    }

    private static void HandleSlipperyPower(SlipperyPower power, AfterDamageReceivedHookContext context)
    {
        if (context.Target == power.Owner && context.Result.UnblockedDamage >= 1)
        {
            // TODO: Simulate SlipperyPower's effect of reducing the power's amount by 1 when the owner takes unblocked damage.
            context.MarkCurrentSourceRisky();
        }
    }

    private static void HandleTheGambitPower(TheGambitPower power, AfterDamageReceivedHookContext context)
    {
        if (context.Target == power.Owner &&
            context.Props.IsPoweredAttack() &&
            context.Result.UnblockedDamage > 0)
        {
            // TODO: Vanilla kills the owner here.
            context.MarkCurrentSourceRisky();
        }
    }
}

internal sealed class CentennialPuzzlePredictionState
{
    public bool UsedThisCombat { get; set; }
}

internal sealed class BeforeDamageReceivedHookContext : CombatPredictionHookContext
{
    public required Creature Target { get; init; }

    public required ValueProp Props { get; init; }

    public required Creature? Dealer { get; init; }

    public required PredictedCard? Source { get; init; }
}

internal sealed class AfterDamageReceivedHookContext : CombatPredictionHookContext
{
    public required Creature Target { get; init; }

    public required DamageResult Result { get; init; }

    public required ValueProp Props { get; init; }

    public required Creature? Dealer { get; init; }

    public required PredictedCard? Source { get; init; }
}
