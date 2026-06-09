using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using RandomForeseer.Common;
using RandomForeseer.InCombat.Hooks;

namespace RandomForeseer.InCombat;

internal interface IDamageBlockExecutor
{
    void Execute(AttackCommand attackCommand);

    void Damage(
        IReadOnlyList<Creature> targets,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource = null);

    void GainBlock(Creature creature, decimal amount, ValueProp props, CardModel? cardSource = null);
}

// Lightweight drift-risk detector for damage/block chains. This intentionally mirrors only
// prediction-relevant risk gates and never mutates combat state or advances RNG.
internal sealed class DamageBlockRiskDetector(ICombatState combatState) : IDamageBlockExecutor
{
    private readonly PredictionStateStore _stateStore = new();

    private readonly PredictionRiskTracker _tracker = new();

    public static PredictionRisk DetectGainBlock(CardModel card)
    {
        if (card.Owner.Creature.CombatState is not { } combatState)
        {
            return PredictionRisk.None;
        }

        var detector = new DamageBlockRiskDetector(combatState);
        detector.GainBlock(
            card.Owner.Creature,
            card.DynamicVars.Block.BaseValue,
            card.DynamicVars.Block.Props,
            cardSource: card);
        return detector.Snapshot();
    }

    public static PredictionRisk DetectAttack(CardModel card, int hitCount = 1)
    {
        if (card.Owner.Creature.CombatState is not { } combatState)
        {
            return PredictionRisk.None;
        }

        var detector = new DamageBlockRiskDetector(combatState);
        detector.Execute(
            DamageCmd.Attack(card.DynamicVars.Damage.BaseValue)
                .FromCard(card)
                .WithHitCount(hitCount));
        return detector.Snapshot();
    }

    public PredictionRisk Snapshot()
    {
        return _tracker.Snapshot();
    }

    public void Execute(AttackCommand attackCommand)
    {
        _tracker.Add(attackCommand.ModelSource);
    }

    public void Damage(IReadOnlyList<Creature> targets, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource = null)
    {
        if (dealer?.IsDead == true || targets.Count == 0)
        {
            return;
        }

        _tracker.Add(cardSource);
    }

    public void GainBlock(Creature creature, decimal amount, ValueProp props, CardModel? cardSource = null)
    {
        if (amount <= 0m)
        {
            return;
        }

        // Hook.ModifyBlock is used by vanilla card previews, so it is treated as a safe read-only value path.
        var modifiedBlock = Hook.ModifyBlock(
            combatState,
            creature,
            amount,
            props,
            cardSource,
            null,
            out _);

        if (modifiedBlock <= 0m)
        {
            return;
        }

        var context = new BlockHookContext
        {
            RiskTracker = _tracker,
            Executor = this,
            StateStore = _stateStore,
            CombatState = combatState,
            Creature = creature,
            Amount = modifiedBlock,
            Props = props,
            Source = cardSource
        };

        // BeforeBlockGained and AfterModifyingBlockAmount currently have no vanilla implementations
        // that can move prediction RNG; leave them unregistered until that changes.
        BlockHooks.RunAfterBlockGained(context);
    }
}
