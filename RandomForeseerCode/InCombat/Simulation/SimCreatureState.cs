using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.ValueProps;

namespace RandomForeseer.RandomForeseerCode.InCombat.Simulation;

internal sealed class SimCreatureState(Creature creature)
{
    public Creature Creature { get; } = creature;

    public int CurrentHp { get; private set; } = creature.CurrentHp;

    public int MaxHp { get; } = creature.MaxHp;

    public int Block { get; private set; } = creature.Block;

    public bool IsAlive => CurrentHp > 0;

    public bool IsDead => !IsAlive;

    public bool IsHittable => IsAlive && Hook.ShouldAllowHitting(Creature.CombatState!, Creature);

    public decimal DamageBlock(decimal amount, ValueProp props)
    {
        var blockedDamage = props.HasFlag(ValueProp.Unblockable)
            ? 0m
            : Math.Min(Block, amount);

        Block -= (int)blockedDamage;
        return blockedDamage;
    }

    public DamageResult LoseHp(decimal amount, ValueProp props)
    {
        var wasTargetKilled = CurrentHp > 0 && amount >= CurrentHp;
        var previousHp = CurrentHp;
        var damage = (int)Math.Min(amount, 999999999m);
        CurrentHp = Math.Max(CurrentHp - damage, 0);

        return new DamageResult(Creature, props)
        {
            UnblockedDamage = previousHp - CurrentHp,
            WasTargetKilled = wasTargetKilled,
            OverkillDamage = wasTargetKilled ? Math.Max(damage - previousHp, 0) : 0
        };
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
