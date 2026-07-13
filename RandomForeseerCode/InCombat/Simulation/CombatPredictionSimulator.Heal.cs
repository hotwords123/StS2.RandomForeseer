using MegaCrit.Sts2.Core.Entities.Creatures;
using RandomForeseer.RandomForeseerCode.InCombat.Mirrors;

namespace RandomForeseer.RandomForeseerCode.InCombat.Simulation;

internal sealed partial class CombatPredictionSimulator
{
    // Mirrors CreatureCmd.Heal's state mutation and HP-change hook without mutating real Creature state.
    // VFX/SFX, map-point history, waits, and player hook activation on revive are intentionally omitted.
    public void Heal(Creature creature, decimal amount)
    {
        var creatureState = State.GetCreature(creature);
        creatureState.Heal(amount);

        if (amount > 0m)
        {
            HookMirrors.AfterCurrentHpChanged(this, creature, amount);
        }
    }
}
