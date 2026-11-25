using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.ValueProps;
using RandomForeseer.InCombat.Mirrors;

namespace RandomForeseer.InCombat.Simulation;

internal sealed class SimCreatureState(CombatPredictionSimulator simulator, Creature creature)
{
    public Creature Creature { get; } = creature;

    public int CurrentHp { get; private set; } = creature.CurrentHp;

    public int MaxHp { get; } = creature.MaxHp;

    public int Block { get; private set; } = creature.Block;

    public HpDisplay HpDisplay { get; set; } = creature.HpDisplay;

    public bool IsAlive => CurrentHp > 0;

    public bool IsDead => !IsAlive;

    public bool IsHittable => IsAlive && HookMirrors.ShouldAllowHitting(simulator, Creature);

    // Vanilla checks CombatState != null here; default creature removal clears that reference.
    // Prediction-side removal only excludes the creature from State.Creatures, leaving the live reference intact.
    // Therefore, we check membership in State.Creatures instead.
    public bool CanReceivePowers => simulator.State.Creatures.Contains(Creature) &&
                                    HookMirrors.ShouldAllowHitting(simulator, Creature);

    /// <summary>Mirrors <see cref="Creature.DamageBlockInternal"/>.</summary>
    public decimal DamageBlock(decimal amount, ValueProp props, bool dryRun = false)
    {
        var blockedDamage = props.HasFlag(ValueProp.Unblockable)
            ? 0m
            : Math.Min(Block, amount);
        if (!dryRun)
        {
            Block -= (int)blockedDamage;
        }

        return blockedDamage;
    }

    /// <summary>Mirrors <see cref="Creature.LoseHpInternal"/>.</summary>
    public HpLossResult LoseHp(decimal amount, bool dryRun = false)
    {
        var previousHp = CurrentHp;
        var wasTargetKilled = previousHp > 0 && amount >= previousHp;
        var damage = (int)Math.Clamp(amount, 0m, 999999999m);
        var remainingHp = Math.Max(previousHp - damage, 0);
        if (!dryRun)
        {
            CurrentHp = remainingHp;
        }

        return new HpLossResult
        {
            hpLost = previousHp - remainingHp,
            killed = wasTargetKilled,
            overkillAmount = wasTargetKilled ? Math.Max(damage - previousHp, 0) : 0
        };
    }

    public void LoseBlock(decimal amount)
    {
        if (amount < 0m)
        {
            throw new ArgumentException("amount must be positive. Use GainBlock for block gain.", nameof(amount));
        }

        Block = (int)Math.Max(Block - amount, 0m);
    }

    public void GainBlock(decimal amount)
    {
        if (amount < 0m)
        {
            throw new ArgumentException("amount must be positive. Use LoseBlock for block loss.", nameof(amount));
        }

        Block = (int)Math.Min(Block + amount, 999999999m);
    }

    public void Heal(decimal amount)
    {
        if (amount < 0m)
        {
            throw new ArgumentException("amount must be positive.", nameof(amount));
        }

        CurrentHp = (int)Math.Min(CurrentHp + amount, MaxHp);
    }
}
