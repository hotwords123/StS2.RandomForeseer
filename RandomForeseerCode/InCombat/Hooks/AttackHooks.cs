using MegaCrit.Sts2.Core.Commands.Builders;
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
using RandomForeseer.RandomForeseerCode.Common.Hooks;

namespace RandomForeseer.RandomForeseerCode.InCombat.Hooks;

internal static class AttackHooks
{
    private static readonly HookSpec BeforeAttack = new(
        nameof(AbstractModel.BeforeAttack),
        [typeof(AttackCommand)]);

    private static readonly HookSpec AfterAttack = new(
        nameof(AbstractModel.AfterAttack),
        [typeof(PlayerChoiceContext), typeof(AttackCommand)]);

    private static readonly HookSpec ModifyAttackHitCount = new(
        nameof(AbstractModel.ModifyAttackHitCount),
        [typeof(AttackCommand), typeof(int)]);

    private static readonly HookRegistry<AttackHookContext> BeforeAttackRegistry =
        CreateBeforeAttackRegistry();

    private static readonly HookRegistry<AttackHookContext> AfterAttackRegistry =
        CreateAfterAttackRegistry();

    private static readonly HookRegistry<ModifyAttackHitCountHookContext> ModifyAttackHitCountRegistry =
        new(ModifyAttackHitCount);

    public static void RunBefore(AttackHookContext context)
    {
        BeforeAttackRegistry.Run(context.CombatState.IterateHookListeners(), context);
    }

    public static void RunAfter(AttackHookContext context)
    {
        AfterAttackRegistry.Run(context.CombatState.IterateHookListeners(), context);
    }

    public static int ModifyHitCount(ModifyAttackHitCountHookContext context)
    {
        ModifyAttackHitCountRegistry.Run(context.CombatState.IterateHookListeners(), context);
        return context.HitCount;
    }

    private static HookRegistry<AttackHookContext> CreateBeforeAttackRegistry()
    {
        var registry = new HookRegistry<AttackHookContext>(BeforeAttack);

        registry.Register<GigantificationPower>(HandleGigantificationBeforeAttack);
        registry.RegisterIgnored<HellraiserPower>();
        registry.Register<VigorPower>(HandleVigorBeforeAttack);

        return registry;
    }

    private static HookRegistry<AttackHookContext> CreateAfterAttackRegistry()
    {
        var registry = new HookRegistry<AttackHookContext>(AfterAttack);

        registry.Register<BoneFlute>(HandleBoneFlute);
        registry.Register<Flatten>(HandleFlatten);
        registry.Register<GigantificationPower>(HandleGigantificationAfterAttack);
        registry.Register<PainfulStabsPower>(HandlePainfulStabsPower);
        registry.Register<SkittishPower>(HandleSkittishPower);
        registry.Register<SuckPower>(HandleSuckPower);
        registry.Register<VigorPower>(HandleVigorAfterAttack);

        return registry;
    }

    private static void HandleGigantificationBeforeAttack(
        GigantificationPower power,
        AttackHookContext context)
    {
        if (ShouldGigantificationTrigger(power, context.Command))
        {
            context.StateStore.Get<AttackPowerPredictionState>(power).DidTrigger = true;
        }
    }

    private static void HandleVigorBeforeAttack(VigorPower power, AttackHookContext context)
    {
        if (ShouldVigorTrigger(power, context.Command))
        {
            context.StateStore.Get<AttackPowerPredictionState>(power).DidTrigger = true;
        }
    }

    private static void HandleBoneFlute(BoneFlute relic, AttackHookContext context)
    {
        if (context.Command.Attacker?.Monster is not Osty ||
            context.Command.Attacker.PetOwner != relic.Owner)
        {
            return;
        }

        context.Simulator.GainBlock(relic.Owner.Creature, relic.DynamicVars.Block);
    }

    private static void HandleFlatten(Flatten card, AttackHookContext context)
    {
        if (context.Command.Attacker == null || context.Command.Attacker != card.Owner.Osty)
        {
            return;
        }

        context.State.FindCard(card)?.MutablePreview.EnergyCost.SetThisTurn(0);
    }

    private static void HandleGigantificationAfterAttack(
        GigantificationPower power,
        AttackHookContext context)
    {
        var state = context.StateStore.Get<AttackPowerPredictionState>(power);
        if (state.DidTrigger && ShouldGigantificationTrigger(power, context.Command))
        {
            state.DidTrigger = false;
            context.MarkCurrentSourceRisky();
        }
    }

    private static void HandlePainfulStabsPower(PainfulStabsPower power, AttackHookContext context)
    {
        if (context.Command.Attacker != power.Owner ||
            context.Command.TargetSide == power.Owner.Side ||
            !context.Command.DamageProps.IsPoweredAttack())
        {
            return;
        }

        var damageResultsByPlayer = context.Command.Results
            .SelectMany(results => results)
            .Where(result => result.Receiver.IsPlayer)
            .GroupBy(result => result.Receiver);

        foreach (var group in damageResultsByPlayer)
        {
            var woundCount = group.Count(result => result.UnblockedDamage > 0) * power.Amount;
            context.Simulator.AddToCombat<Wound>(group.Key, PileType.Discard, woundCount, creator: null);
        }
    }

    private static void HandleSkittishPower(SkittishPower power, AttackHookContext context)
    {
        var state = context.StateStore.Get(power, () => new SkittishPredictionState
        {
            HasGainedBlockThisTurn = power.HasGainedBlockThisTurn
        });

        if (state.HasGainedBlockThisTurn ||
            !context.Command.DamageProps.HasFlag(ValueProp.Move) ||
            context.Command.ModelSource is not CardModel)
        {
            return;
        }

        var damageResult = context.Command.Results
            .SelectMany(results => results)
            .FirstOrDefault(result => result.Receiver == power.Owner);
        if (damageResult is not { UnblockedDamage: > 0 })
        {
            return;
        }

        state.HasGainedBlockThisTurn = true;
        context.Simulator.GainBlock(power.Owner, power.Amount, ValueProp.Unpowered);
    }

    private static void HandleSuckPower(SuckPower power, AttackHookContext context)
    {
        if (context.Command.Attacker != power.Owner ||
            context.Command.TargetSide == power.Owner.Side ||
            !context.Command.DamageProps.IsPoweredAttack())
        {
            return;
        }

        var triggeredHits = 0;

        foreach (var hitResults in context.Command.Results)
        {
            var petOwners = hitResults
                .Where(result => result.Receiver.IsPet)
                .Select(result => result.Receiver.PetOwner?.Creature)
                .OfType<Creature>()
                .ToHashSet();

            if (hitResults.Any(result => result.UnblockedDamage > 0 && !petOwners.Contains(result.Receiver)))
            {
                triggeredHits++;
            }
        }

        if (triggeredHits > 0)
        {
            context.MarkCurrentSourceRisky();
        }
    }

    private static void HandleVigorAfterAttack(VigorPower power, AttackHookContext context)
    {
        var state = context.StateStore.Get<AttackPowerPredictionState>(power);
        if (state.DidTrigger && ShouldVigorTrigger(power, context.Command))
        {
            state.DidTrigger = false;
            context.MarkCurrentSourceRisky();
        }
    }

    private static bool ShouldGigantificationTrigger(GigantificationPower power, AttackCommand command)
    {
        return command.ModelSource is CardModel card &&
            card.Owner.Creature == power.Owner &&
            card.Type == CardType.Attack &&
            command.DamageProps.IsPoweredAttack();
    }

    private static bool ShouldVigorTrigger(VigorPower power, AttackCommand command)
    {
        return command.Attacker == power.Owner &&
            command.DamageProps.IsPoweredAttack() &&
            command.ModelSource is null or CardModel;
    }
}

internal sealed class AttackPowerPredictionState
{
    public bool DidTrigger { get; set; }
}

internal sealed class SkittishPredictionState
{
    public bool HasGainedBlockThisTurn { get; set; }
}

internal sealed class AttackHookContext : CombatPredictionHookContext
{
    public required AttackCommand Command { get; init; }
}

internal sealed class ModifyAttackHitCountHookContext : CombatPredictionHookContext
{
    public required AttackCommand Command { get; init; }

    public required int HitCount { get; set; }
}
