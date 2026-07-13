using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Achievements;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.ValueProps;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.Common.Mirrors;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Hooks.Damage;

using Registry = ModelMethodMirrorRegistry<AbstractModel, AfterDamageGivenMirrorContext>;

internal static class AfterDamageGivenMirrors
{
    private static readonly MirrorMethodSpec AfterDamageGiven = MirrorMethodSpec.Hook(
        nameof(AbstractModel.AfterDamageGiven),
        [
            typeof(PlayerChoiceContext),
            typeof(Creature),
            typeof(DamageResult),
            typeof(ValueProp),
            typeof(Creature),
            typeof(CardModel)
        ]);

    private static readonly Registry Registry = CreateRegistry();

    public static void Invoke(AbstractModel listener, AfterDamageGivenMirrorContext context)
    {
        Registry.Invoke(listener, context);
    }

    private static Registry CreateRegistry()
    {
        var registry = new Registry(AfterDamageGiven);

        registry.RegisterIgnored<SkillIronclad2Achievement>();
        registry.Register<ConcoctPower>(HandleConcoctPower);
        registry.Register<EnvenomPower>(HandleEnvenomPower);
        registry.Register<HandDrill>(HandleHandDrill);
        registry.RegisterIgnored<ImbalancedPower>();
        registry.Register<MonarchsGazePower>(HandleMonarchsGazePower);
        registry.RegisterIgnored<PaperCutsPower>();
        registry.Register<ReaperFormPower>(HandleReaperFormPower);
        registry.Register<SicEmPower>(HandleSicEmPower);
        registry.Register<UnderworldPower>(HandleUnderworldPower);

        return registry;
    }

    private static void HandleConcoctPower(ConcoctPower power, AfterDamageGivenMirrorContext context)
    {
        if (context.Dealer == power.Owner &&
            context.Props.IsPoweredAttack() &&
            context.Result.UnblockedDamage > 0)
        {
            // TODO: Mirror vanilla v0.108.0 ConcoctPower by applying Poison in prediction state.
            context.MarkCurrentSourceRisky();
        }
    }

    private static void HandleEnvenomPower(EnvenomPower power, AfterDamageGivenMirrorContext context)
    {
        if (context.Dealer == power.Owner &&
            context.Props.IsPoweredAttack() &&
            context.Result.UnblockedDamage > 0)
        {
            context.MarkCurrentSourceRisky();
        }
    }

    private static void HandleHandDrill(HandDrill relic, AfterDamageGivenMirrorContext context)
    {
        if ((context.Dealer == relic.Owner.Creature || context.Dealer?.PetOwner == relic.Owner) &&
            !context.Target.IsPlayer &&
            context.Result.WasBlockBroken)
        {
            context.MarkCurrentSourceRisky();
        }
    }

    private static void HandleMonarchsGazePower(MonarchsGazePower power, AfterDamageGivenMirrorContext context)
    {
        if (context.Dealer == power.Owner && context.Props.IsPoweredAttack())
        {
            context.MarkCurrentSourceRisky();
        }
    }

    private static void HandleReaperFormPower(ReaperFormPower power, AfterDamageGivenMirrorContext context)
    {
        if (context.Dealer != null &&
            (context.Dealer == power.Owner || context.Dealer.PetOwner?.Creature == power.Owner) &&
            context.Props.IsPoweredAttack() &&
            context.Result.TotalDamage > 0)
        {
            context.MarkCurrentSourceRisky();
        }
    }

    private static void HandleSicEmPower(SicEmPower power, AfterDamageGivenMirrorContext context)
    {
        if (context.Dealer?.Monster is Osty osty &&
            power.Applier != null &&
            osty.Creature.PetOwner?.Creature == power.Applier &&
            context.Target == power.Owner)
        {
            context.MarkCurrentSourceRisky();
        }
    }

    private static void HandleUnderworldPower(UnderworldPower power, AfterDamageGivenMirrorContext context)
    {
        if (context.Dealer != null &&
            context.Dealer != power.Owner &&
            context.Dealer.PetOwner != power.Owner.Player &&
            context.Props.IsPoweredAttack() &&
            context.Result.TotalDamage > 0)
        {
            // TODO: Mirror vanilla v0.108.0 UnderworldPower by applying Doom in prediction state.
            context.MarkCurrentSourceRisky();
        }
    }
}

internal sealed class AfterDamageGivenMirrorContext : CombatPredictionMirrorContext
{
    public required Creature Target { get; init; }

    public required DamageResult Result { get; init; }

    public required ValueProp Props { get; init; }

    public required Creature? Dealer { get; init; }

    public required PredictedCard? Source { get; init; }
}
