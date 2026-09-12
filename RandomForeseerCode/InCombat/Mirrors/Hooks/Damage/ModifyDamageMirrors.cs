using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.ValueProps;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.Common.Mirrors;
using RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Hooks.Attack;
using RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Hooks.Card;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Hooks.Damage;

using Registry = MethodMirrorRegistry<AbstractModel, ModifyDamageMirrorContext, decimal>;

// Mirrors the additive and multiplicative listener passes inside Hook.ModifyDamage.
internal static class ModifyDamageMirrors
{
    private static readonly MirrorMethodSpec ModifyDamageAdditive = MirrorMethodSpec.Hook(
        nameof(AbstractModel.ModifyDamageAdditive),
        [
            typeof(Creature),
            typeof(decimal),
            typeof(ValueProp),
            typeof(Creature),
            typeof(CardModel),
            typeof(CardPlay)
        ]);

    private static readonly MirrorMethodSpec ModifyDamageMultiplicative = MirrorMethodSpec.Hook(
        nameof(AbstractModel.ModifyDamageMultiplicative),
        [
            typeof(Creature),
            typeof(decimal),
            typeof(ValueProp),
            typeof(Creature),
            typeof(CardModel),
            typeof(CardPlay)
        ]);

    private static readonly Registry AdditiveRegistry = CreateAdditiveRegistry();
    private static readonly Registry MultiplicativeRegistry = CreateMultiplicativeRegistry();

    public static decimal InvokeAdditive(AbstractModel listener, ModifyDamageMirrorContext context)
    {
        return AdditiveRegistry.TryInvokeRegistered(listener, context, out var result)
            ? result.Value
            : InvokeOriginalAdditive(listener, context);
    }

    public static decimal InvokeMultiplicative(AbstractModel listener, ModifyDamageMirrorContext context)
    {
        return MultiplicativeRegistry.TryInvokeRegistered(listener, context, out var result)
            ? result.Value
            : InvokeOriginalMultiplicative(listener, context);
    }

    private static decimal InvokeOriginalAdditive(AbstractModel listener, ModifyDamageMirrorContext context)
    {
        return listener.ModifyDamageAdditive(
            context.Target,
            context.Amount,
            context.Props,
            context.Dealer,
            context.CardSource?.Preview,
            context.CardPlay);
    }

    private static decimal InvokeOriginalMultiplicative(AbstractModel listener, ModifyDamageMirrorContext context)
    {
        return listener.ModifyDamageMultiplicative(
            context.Target,
            context.Amount,
            context.Props,
            context.Dealer,
            context.CardSource?.Preview,
            context.CardPlay);
    }

    private static Registry CreateAdditiveRegistry()
    {
        var registry = new Registry(ModifyDamageAdditive);

        registry.Register<OneForAllPower>(HandleOneForAllPower);
        registry.Register<PhantomBladesPower>(HandlePhantomBladesPower);
        registry.Register<VigorPower>(VigorPowerMirrors.ModifyDamageAdditive);

        return registry;
    }

    private static Registry CreateMultiplicativeRegistry()
    {
        var registry = new Registry(ModifyDamageMultiplicative);

        registry.Register<FlutterPower>(HandleFlutterPower);
        registry.Register<GigantificationPower>(GigantificationPowerMirrors.ModifyDamageMultiplicative);
        registry.Register<LethalityPower>(HandleLethalityPower);
        registry.Register<SlowPower>(HandleSlowPower);
        registry.Register<SurroundedPower>(HandleSurroundedPower);

        registry.Register<PenNib>(HandlePenNib);

        return registry;
    }

    private static decimal HandleOneForAllPower(OneForAllPower power, ModifyDamageMirrorContext context)
    {
        if (!context.Props.IsPoweredAttack() ||
            context.CardSource is not { } card ||
            card.Preview.Owner.Creature != power.Owner ||
            (context.CardPlay?.Card.EnergyCost.CostsX ?? card.Preview.EnergyCost.CostsX))
        {
            return 0;
        }

        var energyCost = context.CardPlay is { } cardPlay
            ? cardPlay.Resources.EnergySpent
            : card.GetEnergyCostWithModifiers(context.Simulator, context.State.GetPlayerCombatState(card.Preview.Owner));
        return energyCost == 0 ? power.Amount : 0;
    }

    private static decimal HandlePhantomBladesPower(PhantomBladesPower power, ModifyDamageMirrorContext context)
    {
        if (!context.Props.IsPoweredAttack() ||
            context.CardSource?.Preview.Tags.Contains(CardTag.Shiv) != true ||
            context.Dealer != power.Owner)
        {
            return 0;
        }

        var shivFinished = CombatManager.Instance.History.CardPlaysFinished.Any(entry =>
                entry.HappenedThisTurn(context.CombatState) && entry.CardPlay.Player == power.Owner.Player &&
                entry.CardPlay.Card.Tags.Contains(CardTag.Shiv)) ||
            context.History.OfType<CombatPredictionCardPlayFinishedEntry>().Any(entry =>
                entry.CardPlay.Player == power.Owner.Player && entry.Card.Preview.Tags.Contains(CardTag.Shiv));
        return shivFinished ? 0 : power.Amount;
    }

    private static decimal HandleLethalityPower(LethalityPower power, ModifyDamageMirrorContext context)
    {
        var card = context.CardSource;
        if (!context.Props.IsPoweredAttack() || card is null || card.Preview.Owner.Creature != power.Owner)
        {
            return 1;
        }

        var isInPlay = card.GetPile(context.State)?.Type == PileType.Play;
        if (isInPlay && card.Preview.CurrentPlayIndex > 0)
        {
            return 1;
        }

        var attacksStarted = CombatManager.Instance.History.CardPlaysStarted.Count(entry =>
                entry.HappenedThisTurn(context.CombatState) && entry.CardPlay.Player == power.Owner.Player &&
                entry.CardPlay.Card.Type == CardType.Attack) +
            context.History.OfType<CombatPredictionCardPlayStartedEntry>().Count(entry =>
                entry.CardPlay.Player == power.Owner.Player && entry.Card.Preview.Type == CardType.Attack);
        return attacksStarted > (isInPlay ? 1 : 0) ? 1 : 1 + power.Amount / 100m;
    }

    private static decimal HandleFlutterPower(FlutterPower power, ModifyDamageMirrorContext context)
    {
        return context.StateStore.GetPowerAmount(power).IsActive
            ? InvokeOriginalMultiplicative(power, context)
            : 1;
    }

    private static decimal HandleSlowPower(SlowPower power, ModifyDamageMirrorContext context)
    {
        if (context.Target != power.Owner || !context.Props.IsPoweredAttack())
        {
            return 1;
        }

        var amount = context.StateStore.Get(power,
            () => new CounterPredictionState(power.DynamicVars["SlowAmount"].IntValue)).Value;
        return 1 + 0.1m * amount;
    }

    private static decimal HandleSurroundedPower(SurroundedPower power, ModifyDamageMirrorContext context)
    {
        if (context.Dealer is null || context.Target != power.Owner)
        {
            return 1;
        }

        var facing = context.StateStore.Get(power, () => new SurroundedPredictionState(power)).Facing;
        return facing switch
        {
            SurroundedPower.Direction.Right when context.Dealer.HasPower<BackAttackLeftPower>() => 1.5m,
            SurroundedPower.Direction.Left when context.Dealer.HasPower<BackAttackRightPower>() => 1.5m,
            _ => 1
        };
    }

    private static decimal HandlePenNib(PenNib relic, ModifyDamageMirrorContext context)
    {
        if (!context.Props.IsPoweredAttack() ||
            context.CardSource is null ||
            context.Dealer != relic.Owner.Creature && context.Dealer != relic.Owner.Osty)
        {
            return 1;
        }

        var state = context.StateStore.Get(relic, () => new PenNibPredictionState(relic));
        if (state.AttackToDouble is not null)
        {
            return state.AttackToDouble == context.CardSource.Original ? 2 : 1;
        }

        return context.CardPlay is null &&
            context.CardSource.GetPile(context.State)?.Type is not PileType.Play &&
            state.AttacksPlayed == 9
                ? 2
                : 1;
    }
}

internal sealed class ModifyDamageMirrorContext : CombatMirrorContext
{
    public required Creature? Target { get; init; }

    public required Creature? Dealer { get; init; }

    public required decimal Amount { get; set; }

    public required ValueProp Props { get; init; }

    public required PredictedCard? CardSource { get; init; }

    public required CardPlay? CardPlay { get; init; }
}
