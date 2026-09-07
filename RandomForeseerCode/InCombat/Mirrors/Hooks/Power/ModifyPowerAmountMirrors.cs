using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.Common.Mirrors;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Hooks.Power;

using DecimalRegistry = MethodMirrorRegistry<AbstractModel, ModifyPowerAmountMirrorContext, decimal>;
using ReceivedRegistry = MethodMirrorRegistry<AbstractModel, ModifyPowerAmountMirrorContext, PowerAmountModification>;

/// <summary>
/// Mirrors the additive, multiplicative and received passes of Hook power-amount modification.
/// </summary>
internal static class ModifyPowerAmountMirrors
{
    private static readonly MirrorMethodSpec ModifyGivenAdditive = MirrorMethodSpec.Hook(
        nameof(AbstractModel.ModifyPowerAmountGivenAdditive),
        [typeof(PowerModel), typeof(Creature), typeof(decimal), typeof(Creature), typeof(CardModel)]);

    private static readonly MirrorMethodSpec ModifyGivenMultiplicative = MirrorMethodSpec.Hook(
        nameof(AbstractModel.ModifyPowerAmountGivenMultiplicative),
        [typeof(PowerModel), typeof(Creature), typeof(decimal), typeof(Creature), typeof(CardModel)]);

    private static readonly MirrorMethodSpec TryModifyReceived = MirrorMethodSpec.Hook(
        nameof(AbstractModel.TryModifyPowerAmountReceived),
        [typeof(PowerModel), typeof(Creature), typeof(decimal), typeof(Creature), typeof(decimal).MakeByRefType()]);

    private static readonly DecimalRegistry AdditiveRegistry = new(ModifyGivenAdditive);
    private static readonly DecimalRegistry MultiplicativeRegistry = new(ModifyGivenMultiplicative);
    private static readonly ReceivedRegistry ReceivedRegistry = CreateReceivedRegistry();

    public static decimal InvokeGivenAdditive(AbstractModel listener, ModifyPowerAmountMirrorContext context)
    {
        return AdditiveRegistry.TryInvokeRegistered(listener, context, out var result)
            ? result.Value
            : listener.ModifyPowerAmountGivenAdditive(
                context.Power,
                context.Applier!,
                context.Amount,
                context.Target,
                context.CardSource?.Preview);
    }

    public static decimal InvokeGivenMultiplicative(AbstractModel listener, ModifyPowerAmountMirrorContext context)
    {
        return MultiplicativeRegistry.TryInvokeRegistered(listener, context, out var result)
            ? result.Value
            : listener.ModifyPowerAmountGivenMultiplicative(
                context.Power,
                context.Applier!,
                context.Amount,
                context.Target,
                context.CardSource?.Preview);
    }

    public static PowerAmountModification InvokeReceived(
        AbstractModel listener,
        ModifyPowerAmountMirrorContext context)
    {
        if (ReceivedRegistry.TryInvokeRegistered(listener, context, out var result))
        {
            return result.Value;
        }

        var wasModified = listener.TryModifyPowerAmountReceived(
            context.Power,
            context.Target,
            context.Amount,
            context.Applier,
            out var modifiedAmount);
        return new PowerAmountModification(wasModified, modifiedAmount);
    }

    private static ReceivedRegistry CreateReceivedRegistry()
    {
        var registry = new ReceivedRegistry(TryModifyReceived);
        registry.Register<ArtifactPower>(HandleArtifact);
        return registry;
    }

    private static PowerAmountModification HandleArtifact(
        ArtifactPower artifact,
        ModifyPowerAmountMirrorContext context)
    {
        if (!context.StateStore.GetPowerAmount(artifact).IsActive)
        {
            return new PowerAmountModification(false, context.Amount);
        }

        var wasModified = artifact.TryModifyPowerAmountReceived(
            context.Power,
            context.Target,
            context.Amount,
            context.Applier,
            out var modifiedAmount);
        return new PowerAmountModification(wasModified, modifiedAmount);
    }
}

internal readonly record struct PowerAmountModification(bool WasModified, decimal Amount);

internal sealed class ModifyPowerAmountMirrorContext : CombatMirrorContext
{
    public required PowerModel Power { get; init; }

    public required Creature Target { get; init; }

    public required Creature? Applier { get; init; }

    public required PredictedCard? CardSource { get; init; }

    public required decimal Amount { get; set; }
}
