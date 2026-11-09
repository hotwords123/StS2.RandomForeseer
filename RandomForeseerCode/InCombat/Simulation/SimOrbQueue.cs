using MegaCrit.Sts2.Core.Entities.Orbs;
using MegaCrit.Sts2.Core.Models;
using RandomForeseer.InCombat.Mirrors.Orbs;

namespace RandomForeseer.InCombat.Simulation;

/// <summary>
/// Mirrors <see cref="OrbQueue"/> without mutating real orb queue.
/// </summary>
internal sealed class SimOrbQueue(OrbQueue liveOrbQueue)
{
    // The simulator clones the real orb queue at the start of a prediction and mutates the clone
    // during simulation. Since the number of orbs is typically small, this is not likely to be a
    // performance concern.
    private readonly List<OrbModel> _orbs = [.. liveOrbQueue.Orbs.Select(orb => (OrbModel)orb.MutableClone())];

    public IReadOnlyList<OrbModel> Orbs => _orbs;

    public int Capacity { get; private set; } = liveOrbQueue.Capacity;

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

    /// <summary>
    /// Mirrors the prediction-relevant orb snapshot and trigger order of <see cref="OrbQueue.BeforeTurnEnd"/>.
    /// </summary>
    public void BeforeTurnEnd(CombatPredictionSimulator simulator)
    {
        foreach (var orb in Orbs.ToList())
        {
            OrbMirrors.InvokeBeforeTurnEndOrbTrigger(simulator, orb);
        }
    }
}
