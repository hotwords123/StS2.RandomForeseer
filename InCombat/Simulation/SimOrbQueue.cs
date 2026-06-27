using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace RandomForeseer.InCombat.Simulation;

internal sealed class SimOrbQueue(Player player)
{
    // Mirrors PlayerCombatState.OrbQueue without mutating real PlayerCombatState.
    // The simulator clones the real orb queue at the start of a prediction and mutates the clone
    // during simulation. Since the number of orbs is typically small, this is not likely to be a
    // performance concern.
    private readonly List<OrbModel> _orbs = player.PlayerCombatState?.OrbQueue.Orbs
        .Select(orb => (OrbModel)orb.MutableClone())
        .ToList() ?? [];

    public IReadOnlyList<OrbModel> Orbs => _orbs;

    public int Capacity { get; private set; } = player.PlayerCombatState?.OrbQueue.Capacity ?? 0;

    public void Clear()
    {
        _orbs.Clear();
        Capacity = 0;
    }

    public void AddCapacity(int capacity)
    {
        Capacity += capacity;
    }

    public void RemoveCapacity(int capacity)
    {
        Capacity = Math.Max(0, Capacity - capacity);
        while (Orbs.Count > Capacity)
        {
            Remove(_orbs.Last());
        }
    }

    public bool Remove(OrbModel orb)
    {
        return _orbs.Remove(orb);
    }

    public bool TryEnqueue(OrbModel orb)
    {
        if (Capacity == 0)
        {
            return false;
        }

        orb.AssertMutable();
        if (Orbs.Count >= Capacity)
        {
            throw new InvalidOperationException("OrbQueue is full");
        }

        _orbs.Add(orb);
        return true;
    }

    public void Insert(int idx, OrbModel orb)
    {
        if (idx >= Capacity)
        {
            throw new InvalidOperationException("idx cannot be greater than capacity");
        }

        _orbs.Insert(idx, orb);
    }
}
