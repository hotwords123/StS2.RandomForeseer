using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using RandomForeseer.InCombat.Hooks;

namespace RandomForeseer.InCombat.Simulation;

internal sealed partial class CombatPredictionSimulator
{
    // Convenience overload for GainBlock with a BlockVar.
    public decimal GainBlock(Creature creature, BlockVar blockVar, CardModel? cardSource = null)
    {
        return GainBlock(creature, blockVar.BaseValue, blockVar.Props, cardSource);
    }

    // Mirrors CreatureCmd.GainBlock without mutating real Creature state.
    public decimal GainBlock(Creature creature, decimal amount, ValueProp props, CardModel? cardSource = null)
    {
        if (amount <= 0m)
        {
            return 0m;
        }

        // Vanilla first checks CombatManager.IsOverOrEnding. The simulator is detached from
        // CombatManager end-state and is only called from live prediction paths.
        var beforeContext = new BlockHookContext
        {
            Simulator = this,
            Creature = creature,
            Amount = amount
        };
        BlockHooks.RunBeforeBlockGained(beforeContext);

        var modifiedBlock = Hook.ModifyBlock(
            State.CombatState,
            creature,
            amount,
            props,
            cardSource,
            null,
            out var modifiers);
        // Hook.ModifyBlock is used by vanilla card previews, so it is treated as a safe
        // read-only value path. AfterModifyingBlockAmount is not mirrored in Phase 1.
        _ = modifiers;

        if (modifiedBlock <= 0m)
        {
            return 0m;
        }

        State.GetCreature(creature).GainBlock(modifiedBlock);

        // Vanilla records BlockGained history before AfterBlockGained. Preview does not mutate
        // run/combat history, but it still scans AfterBlockGained through BlockHooks below so
        // known block-triggered state changes can be mirrored or marked as risk.
        var afterContext = new BlockHookContext
        {
            Simulator = this,
            Creature = creature,
            Amount = modifiedBlock
        };

        BlockHooks.RunAfterBlockGained(afterContext);
        return modifiedBlock;
    }
}
