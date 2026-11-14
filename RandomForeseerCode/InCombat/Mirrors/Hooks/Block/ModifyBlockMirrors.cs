using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using RandomForeseer.Common.Mirrors;
using RandomForeseer.Common;
using RandomForeseer.InCombat.Mirrors.Hooks.Card;
using RandomForeseer.InCombat.Simulation;

namespace RandomForeseer.InCombat.Mirrors.Hooks.Block;

using Registry = MethodMirrorRegistry<AbstractModel, ModifyBlockMirrorContext, decimal>;

/// <summary>
/// Mirrors the additive and multiplicative listener passes inside <see cref="Hook.ModifyBlock"/>.
/// </summary>
internal static class ModifyBlockMirrors
{
    private static readonly MirrorMethodSpec ModifyBlockAdditive = MirrorMethodSpec.Hook(
        nameof(AbstractModel.ModifyBlockAdditive),
        [typeof(Creature), typeof(decimal), typeof(ValueProp), typeof(CardModel), typeof(CardPlay)]);

    private static readonly MirrorMethodSpec ModifyBlockMultiplicative = MirrorMethodSpec.Hook(
        nameof(AbstractModel.ModifyBlockMultiplicative),
        [typeof(Creature), typeof(decimal), typeof(ValueProp), typeof(CardModel), typeof(CardPlay)]);

    private static readonly Registry AdditiveRegistry = CreateAdditiveRegistry();
    private static readonly Registry MultiplicativeRegistry = CreateMultiplicativeRegistry();

    public static decimal InvokeAdditive(AbstractModel listener, ModifyBlockMirrorContext context)
    {
        return AdditiveRegistry.TryInvokeRegistered(listener, context, out var result)
            ? result.Value
            : InvokeOriginalAdditive(listener, context);
    }

    public static decimal InvokeMultiplicative(AbstractModel listener, ModifyBlockMirrorContext context)
    {
        return MultiplicativeRegistry.TryInvokeRegistered(listener, context, out var result)
            ? result.Value
            : InvokeOriginalMultiplicative(listener, context);
    }

    private static decimal InvokeOriginalAdditive(AbstractModel listener, ModifyBlockMirrorContext context)
    {
        return listener.ModifyBlockAdditive(
            context.Target,
            context.Amount,
            context.Props,
            context.CardSource?.Preview,
            context.CardPlay);
    }

    private static decimal InvokeOriginalMultiplicative(AbstractModel listener, ModifyBlockMirrorContext context)
    {
        return listener.ModifyBlockMultiplicative(
            context.Target,
            context.Amount,
            context.Props,
            context.CardSource?.Preview,
            context.CardPlay);
    }

    private static Registry CreateAdditiveRegistry()
    {
        var registry = new Registry(ModifyBlockAdditive);
        registry.Register<StretchPower>(HandleStretchPower);
        return registry;
    }

    private static Registry CreateMultiplicativeRegistry()
    {
        var registry = new Registry(ModifyBlockMultiplicative);

        registry.Register<PaelsLegion>(HandlePaelsLegion);
        registry.Register<Vambrace>(HandleVambrace);
        registry.Register<UnmovablePower>(HandleUnmovablePower);

        return registry;
    }

    private static decimal HandleStretchPower(StretchPower power, ModifyBlockMirrorContext context)
    {
        if (context.CardSource is not { } source || source.Preview.Owner != power.Owner.Player ||
            !context.Props.IsPoweredCardOrMonsterMoveBlock())
        {
            return 0;
        }

        var state = context.StateStore.Get(power, () => new StretchPredictionState(power));
        if (state.Play is not null && state.Play != context.CardPlay)
        {
            return 0;
        }

        var amount = context.StateStore.GetPowerAmount(power).Amount;
        if (context.CardPlay is null)
        {
            return amount;
        }

        if (state.Play is null || state.Play.Player != source.Preview.Owner)
        {
            return 0;
        }

        state.GaveBonus = true;
        return amount;
    }

    private static decimal HandleUnmovablePower(UnmovablePower power, ModifyBlockMirrorContext context)
    {
        if (context.Target.IsMonster || !context.Props.IsCardOrMonsterMove() ||
            (context.CardSource is not null && context.CardSource.Preview.Owner.Creature != power.Owner))
        {
            return 1;
        }

        var count = CombatManager.Instance.History.Entries.OfType<BlockGainedEntry>().Count(entry =>
            entry.HappenedThisTurn(context.CombatState) && entry.CardPlay is { } play &&
            play.Player.Creature == power.Owner && entry.Props.IsCardOrMonsterMove() && play != context.CardPlay);
        count += context.History.OfType<CombatPredictionBlockGainedEntry>().Count(entry =>
            entry.CardPlay is { } play && play.Player.Creature == power.Owner &&
            entry.Props.IsCardOrMonsterMove() && play != context.CardPlay);
        return count < context.StateStore.GetPowerAmount(power).Amount ? 2 : 1;
    }

    private static decimal HandlePaelsLegion(
        PaelsLegion relic,
        ModifyBlockMirrorContext context)
    {
        var state = context.StateStore.Get(relic, () => new PaelsLegionPredictionState(relic));
        return context.Props.IsCardOrMonsterMove() &&
            context.CardSource?.Preview.Owner == relic.Owner &&
            state.Cooldown <= 0 &&
            (state.AffectedCardPlay is null || state.AffectedCardPlay == context.CardPlay)
                ? 2
                : 1;
    }

    private static decimal HandleVambrace(Vambrace relic, ModifyBlockMirrorContext context)
    {
        var state = context.StateStore.Get(relic, () => new VambracePredictionState(relic));
        return context.Props.IsCardOrMonsterMove() &&
            context.CardSource?.Preview.Owner == relic.Owner &&
            !state.BlockGainedThisCombat &&
            (state.TriggeringCard is null || context.CardSource.Original == state.TriggeringCard)
                ? 2
                : 1;
    }
}

internal sealed class ModifyBlockMirrorContext : CombatMirrorContext
{
    public required Creature Target { get; init; }

    public required decimal Amount { get; set; }

    public required ValueProp Props { get; init; }

    public required PredictedCard? CardSource { get; init; }

    public required CardPlay? CardPlay { get; init; }
}
