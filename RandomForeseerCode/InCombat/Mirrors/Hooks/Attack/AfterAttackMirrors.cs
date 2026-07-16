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
using RandomForeseer.RandomForeseerCode.Common.Mirrors;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Hooks.Attack;

using Registry = ModelMethodMirrorRegistry<AbstractModel, AfterAttackMirrorContext>;

// Mirrors the prediction-relevant parts of Hook.AfterAttack.
internal static class AfterAttackMirrors
{
    private static readonly MirrorMethodSpec AfterAttack = MirrorMethodSpec.Hook(
        nameof(AbstractModel.AfterAttack),
        [typeof(PlayerChoiceContext), typeof(AttackCommand)]);

    private static readonly Registry Registry = CreateRegistry();

    public static void Invoke(AbstractModel listener, AfterAttackMirrorContext context)
    {
        Registry.Invoke(listener, context);
    }

    private static Registry CreateRegistry()
    {
        var registry = new Registry(AfterAttack);

        registry.Register<BoneFlute>(HandleBoneFlute);
        registry.Register<Flatten>(HandleFlatten);
        registry.Register<GigantificationPower>(GigantificationPowerMirrors.AfterAttack);
        registry.Register<PainfulStabsPower>(HandlePainfulStabsPower);
        registry.Register<SkittishPower>(HandleSkittishPower);
        registry.Register<SuckPower>(HandleSuckPower);
        registry.Register<VigorPower>(VigorPowerMirrors.AfterAttack);

        return registry;
    }

    private static void HandleBoneFlute(BoneFlute relic, AfterAttackMirrorContext context)
    {
        if (context.Command.Attacker?.Monster is Osty &&
            context.Command.Attacker.PetOwner == relic.Owner)
        {
            context.Simulator.GainBlock(relic.Owner.Creature, relic.DynamicVars.Block);
        }
    }

    private static void HandleFlatten(Flatten card, AfterAttackMirrorContext context)
    {
        if (context.Command.Attacker is not null && context.Command.Attacker == card.Owner.Osty)
        {
            context.State.FindCard(card)?.MutablePreview.EnergyCost.SetThisTurn(0);
        }
    }

    private static void HandlePainfulStabsPower(PainfulStabsPower power, AfterAttackMirrorContext context)
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

    private static void HandleSkittishPower(SkittishPower power, AfterAttackMirrorContext context)
    {
        var state = context.StateStore.Get(power, () => new SkittishPredictionState(power));

        if (state.HasGainedBlockThisTurn ||
            !context.Command.DamageProps.HasFlag(ValueProp.Move) ||
            context.Command.ModelSource is not CardModel)
        {
            return;
        }

        var damageResult = context.Command.Results
            .SelectMany(results => results)
            .FirstOrDefault(result => result.Receiver == power.Owner);
        if (damageResult?.UnblockedDamage > 0)
        {
            state.HasGainedBlockThisTurn = true;
            context.Simulator.GainBlock(power.Owner, power.Amount, ValueProp.Unpowered);
        }
    }

    private static void HandleSuckPower(SuckPower power, AfterAttackMirrorContext context)
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
            context.History.RecordRisk(PredictionRiskReason.MethodMirrorIncomplete);
        }
    }
}

internal sealed class AfterAttackMirrorContext : CombatPredictionMirrorContext
{
    public required AttackCommand Command { get; init; }
}

internal sealed class SkittishPredictionState(SkittishPower power)
{
    public bool HasGainedBlockThisTurn { get; set; } = power.HasGainedBlockThisTurn;
}
