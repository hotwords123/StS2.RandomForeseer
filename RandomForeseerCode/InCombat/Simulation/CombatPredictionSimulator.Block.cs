using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.InCombat.Mirrors;

namespace RandomForeseer.RandomForeseerCode.InCombat.Simulation;

internal sealed partial class CombatPredictionSimulator
{
    /// <summary>
    /// Mirrors <see cref="CreatureCmd.LoseBlock(PlayerChoiceContext, Creature, decimal, Creature)"/>
    /// without mutating live block or firing creature events.
    /// </summary>
    public void LoseBlock(Creature target, decimal amount, Creature? remover)
    {
        var targetState = State.GetCreature(target);
        if (IsOverOrEnding || targetState.IsDead || amount <= 0m)
        {
            return;
        }

        var previousBlock = targetState.Block;
        targetState.LoseBlock(amount);
        if (previousBlock > 0 && targetState.Block <= 0)
        {
            HookMirrors.AfterBlockBroken(this, target, remover);
        }
    }

    /// <summary>
    /// Mirrors <see cref="CreatureCmd.GainBlock(Creature, BlockVar, CardPlay?, bool)"/>.
    /// Convenience overload for when a <see cref="BlockVar"/> is supplied and the block source is not a card play.
    /// </summary>
    public decimal GainBlock(Creature creature, BlockVar blockVar)
    {
        return GainBlock(creature, blockVar.BaseValue, blockVar.Props, cardSource: null, cardPlay: null);
    }

    /// <summary>
    /// Mirrors <see cref="CreatureCmd.GainBlock(Creature, BlockVar, CardPlay?, bool)"/>.
    /// Convenience overload for when a <see cref="BlockVar"/> is supplied.
    /// </summary>
    public decimal GainBlock(Creature creature, BlockVar blockVar, PredictedCard? cardSource, CardPlay? cardPlay)
    {
        return GainBlock(creature, blockVar.BaseValue, blockVar.Props, cardSource, cardPlay);
    }

    /// <summary>
    /// Mirrors <see cref="CreatureCmd.GainBlock(Creature, decimal, ValueProp, CardPlay?, bool)"/>.
    /// Convenience overload for when the block source is not a card play.
    /// </summary>
    public decimal GainBlock(Creature creature, decimal amount, ValueProp props)
    {
        return GainBlock(creature, amount, props, cardSource: null, cardPlay: null);
    }

    /// <summary>
    /// Mirrors <see cref="CreatureCmd.GainBlock(Creature, decimal, ValueProp, CardPlay?, bool)"/>.
    /// </summary>
    public decimal GainBlock(
        Creature creature,
        decimal amount,
        ValueProp props,
        PredictedCard? cardSource,
        CardPlay? cardPlay)
    {
        if (IsOverOrEnding || State.GetCreature(creature).IsDead || amount <= 0m)
        {
            return 0m;
        }

        HookMirrors.BeforeBlockGained(this, creature, amount, props, cardSource);

        var modifiedBlock = HookMirrors.ModifyBlock(
            this,
            creature,
            amount,
            props,
            cardSource,
            cardPlay,
            out var modifiers);
        HookMirrors.AfterModifyingBlockAmount(this, modifiedBlock, cardSource, cardPlay, modifiers);

        if (modifiedBlock <= 0m)
        {
            return 0m;
        }

        State.GetCreature(creature).GainBlock(modifiedBlock);

        // Vanilla records BlockGained history before AfterBlockGained. Preview does not mutate
        // run/combat history, but it still scans AfterBlockGained through HookMirrors below so
        // known block-triggered state changes can be mirrored or marked as risk.
        HookMirrors.AfterBlockGained(this, creature, modifiedBlock, props, cardSource);
        return modifiedBlock;
    }
}
