using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using RandomForeseer.RandomForeseerCode.Common.Mirrors;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Hooks.Attack;

using Registry = ModelMethodMirrorRegistry<AbstractModel, BeforeAttackMirrorContext>;

// Mirrors the prediction-relevant parts of Hook.BeforeAttack.
internal static class BeforeAttackMirrors
{
    private static readonly MirrorMethodSpec BeforeAttack = MirrorMethodSpec.Hook(
        nameof(AbstractModel.BeforeAttack),
        [typeof(AttackCommand)]);

    private static readonly Registry Registry = CreateRegistry();

    public static void Invoke(AbstractModel listener, BeforeAttackMirrorContext context)
    {
        Registry.Invoke(listener, context);
    }

    private static Registry CreateRegistry()
    {
        var registry = new Registry(BeforeAttack);

        registry.Register<GigantificationPower>(GigantificationPowerMirrors.BeforeAttack);
        registry.RegisterIgnored<HellraiserPower>();
        registry.Register<VigorPower>(VigorPowerMirrors.BeforeAttack);

        return registry;
    }
}

internal sealed class BeforeAttackMirrorContext : CombatPredictionMirrorContext
{
    public required AttackCommand Command { get; init; }
}
