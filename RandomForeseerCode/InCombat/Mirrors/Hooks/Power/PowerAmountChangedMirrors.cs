using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.Common.Mirrors;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Hooks.Power;

using Registry = MethodMirrorRegistry<AbstractModel, PowerAmountChangedMirrorContext>;
using AfterRegistry = MethodMirrorRegistry<AbstractModel, AfterModifyingPowerAmountMirrorContext>;

/// <summary>
/// Mirrors prediction-relevant commits from the action Hook stages surrounding a power amount change.
/// </summary>
internal static class PowerAmountChangedMirrors
{
    private static readonly MirrorMethodSpec BeforePowerAmountChanged = MirrorMethodSpec.Hook(
        nameof(AbstractModel.BeforePowerAmountChanged),
        [typeof(PowerModel), typeof(decimal), typeof(Creature), typeof(Creature), typeof(CardModel)]);

    private static readonly MirrorMethodSpec AfterPowerAmountChanged = MirrorMethodSpec.Hook(
        nameof(AbstractModel.AfterPowerAmountChanged),
        [typeof(PlayerChoiceContext), typeof(PowerModel), typeof(decimal), typeof(Creature), typeof(CardModel)]);

    private static readonly MirrorMethodSpec AfterModifyingGiven = MirrorMethodSpec.Hook(
        nameof(AbstractModel.AfterModifyingPowerAmountGiven),
        [typeof(PowerModel)]);

    private static readonly MirrorMethodSpec AfterModifyingReceived = MirrorMethodSpec.Hook(
        nameof(AbstractModel.AfterModifyingPowerAmountReceived),
        [typeof(PowerModel)]);

    private static readonly Registry BeforeRegistry = new(BeforePowerAmountChanged);
    private static readonly Registry AfterRegistry = CreateAfterRegistry();
    private static readonly AfterRegistry AfterGivenRegistry = new(AfterModifyingGiven);
    private static readonly AfterRegistry AfterReceivedRegistry = CreateAfterReceivedRegistry();

    public static void InvokeBefore(AbstractModel listener, PowerAmountChangedMirrorContext context)
    {
        BeforeRegistry.Invoke(listener, context);
    }

    public static void InvokeAfter(AbstractModel listener, PowerAmountChangedMirrorContext context)
    {
        AfterRegistry.Invoke(listener, context);
    }

    public static void InvokeAfterGiven(AbstractModel listener, AfterModifyingPowerAmountMirrorContext context)
    {
        AfterGivenRegistry.Invoke(listener, context);
    }

    public static void InvokeAfterReceived(AbstractModel listener, AfterModifyingPowerAmountMirrorContext context)
    {
        AfterReceivedRegistry.Invoke(listener, context);
    }

    private static Registry CreateAfterRegistry()
    {
        var registry = new Registry(AfterPowerAmountChanged);
        registry.Register<ViciousPower>(HandleVicious);
        return registry;
    }

    private static AfterRegistry CreateAfterReceivedRegistry()
    {
        var registry = new AfterRegistry(AfterModifyingReceived);
        registry.Register<ArtifactPower>(HandleArtifactConsumed);
        return registry;
    }

    private static void HandleVicious(ViciousPower vicious, PowerAmountChangedMirrorContext context)
    {
        var state = context.StateStore.GetPowerAmount(vicious);
        if (state.IsActive &&
            context.Amount > 0m &&
            context.Applier == vicious.Owner &&
            context.Power is VulnerablePower &&
            vicious.Owner.Player is { } player)
        {
            context.Simulator.Draw(player, state.Amount);
        }
    }

    private static void HandleArtifactConsumed(
        ArtifactPower artifact,
        AfterModifyingPowerAmountMirrorContext context)
    {
        context.StateStore.GetPowerAmount(artifact).Decrement();
    }
}

internal sealed class PowerAmountChangedMirrorContext : CombatMirrorContext
{
    public required PowerModel Power { get; init; }

    public required decimal Amount { get; init; }

    public required Creature Target { get; init; }

    public required Creature? Applier { get; init; }

    public required PredictedCard? CardSource { get; init; }
}

internal sealed class AfterModifyingPowerAmountMirrorContext : CombatMirrorContext
{
    public required PowerModel Power { get; init; }
}
