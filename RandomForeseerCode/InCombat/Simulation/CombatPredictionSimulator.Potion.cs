using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using RandomForeseer.Common;
using RandomForeseer.InCombat.Mirrors.Potions.OnUse;

namespace RandomForeseer.InCombat.Simulation;

internal sealed partial class CombatPredictionSimulator
{
    /// <summary>Starts the prediction-relevant portion of one manual potion use.</summary>
    /// <param name="potion">The exact live mutable potion that anchors the prediction trace.</param>
    /// <param name="target">The explicit target, or <see langword="null"/> when vanilla may resolve the owner.</param>
    /// <param name="frame">The exact root potion-use frame when target validation succeeds.</param>
    /// <returns><see langword="true"/> when the simulated use starts; otherwise <see langword="false"/>.</returns>
    /// <remarks>
    /// This mirrors target completion from <see cref="PotionModel.EnqueueManualUse"/> and dispatches only the potion's
    /// <see cref="PotionModel.OnUse"/> body. It intentionally does not call the real potion,
    /// <see cref="PotionModel.OnUseWrapper"/>, commands, choice contexts, removal, hooks, history, VFX, or waits. The
    /// returned frame must be paired only with this simulator's history.
    /// </remarks>
    public bool ManualUse(
        PotionModel potion,
        Creature? target,
        [NotNullWhen(true)] out PredictionTraceFrame? frame)
    {
        var owner = potion.Owner.Creature;
        if (target is null && potion.IsValidTarget(owner))
        {
            target = owner;
        }

        if (!potion.IsValidTarget(target))
        {
            frame = null;
            return false;
        }

        OnUseWrapper(potion, target, out frame);
        return true;
    }

    // Mirrors PotionModel.OnUseWrapper.
    private void OnUseWrapper(PotionModel potion, Creature? target, out PredictionTraceFrame frame)
    {
        using var _ = PushActionSource(potion, PredictionActionKind.PotionUse);
        frame = CurrentFrame ?? throw new UnreachableException("No current frame after pushing action source.");

        // TODO: RemoveBeforeUse
        // TODO: Dispatch Hook.BeforePotionUsed

        PotionOnUseMirrors.Invoke(this, potion, target);

        if (State.GetCreature(potion.Owner.Creature).IsAlive)
        {
            // TODO: Dispatch Hook.AfterPotionUsed
            // TODO: CheckForEmptyHand
        }
    }
}
