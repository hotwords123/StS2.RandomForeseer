using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Hooks;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Cards.OnPlay;

internal static class VulnerableCardMirrors
{
    public static void ShockwaveOnPlay(Shockwave card, CardOnPlayMirrorContext context)
    {
        var amount = card.DynamicVars["Power"].IntValue;
        foreach (var target in context.State.HittableEnemies)
        {
            ApplyWeakAndVulnerable(context, target, amount, amount);
        }
    }

    public static void ExposeOnPlay(Expose card, CardOnPlayMirrorContext context)
    {
        var amount = card.DynamicVars["Power"].IntValue;
        context.Simulator.LoseBlock(context.Target, context.State.GetCreature(context.Target).Block, card.Owner.Creature);
        context.Simulator.RemovePower<ArtifactPower>(context.Target);
        context.ApplyPower<VulnerablePower>(context.Target, amount);
    }

    public static void DominateOnPlay(Dominate card, CardOnPlayMirrorContext context)
    {
        var vulnerable = context.ApplyPower<VulnerablePower>(context.Target);
        vulnerable ??= context.Target.GetPower<VulnerablePower>();

        var amount = vulnerable is not null
            ? Math.Max(0, context.StateStore.GetPowerAmount(vulnerable).Amount)
            : 0;
        context.ApplyPower<StrengthPower>(card.Owner.Creature, amount);
    }

    public static void HighFiveOnPlay(HighFive card, CardOnPlayMirrorContext context)
    {
        if (card.Owner.Osty is not { } osty || !context.State.GetCreature(osty).IsAlive)
        {
            return;
        }

        DamageCmd.Attack(card.DynamicVars.OstyDamage.BaseValue)
            .FromOsty(osty, card, context.CardPlay)
            .TargetingAllOpponents(context.CombatState)
            .Simulate(context.Simulator);

        context.ApplyPower<VulnerablePower>(context.State.HittableEnemies);
    }

    public static void MoltenFistOnPlay(MoltenFist card, CardOnPlayMirrorContext context)
    {
        context.AttackSingle();
        if (!context.State.GetCreature(context.Target).IsAlive ||
            context.Target.GetPower<VulnerablePower>() is not { } vulnerable)
        {
            return;
        }

        var amount = context.StateStore.GetPowerAmount(vulnerable).Amount;
        if (amount > 0)
        {
            context.ApplyPower<VulnerablePower>(context.Target, amount);
        }
    }

    public static void MadScienceSappingOnPlay(MadScience card, CardOnPlayMirrorContext context)
    {
        context.AttackSingle();

        ApplyWeakAndVulnerable(
            context,
            context.Target,
            card.DynamicVars["SappingWeak"].BaseValue,
            card.DynamicVars["SappingVulnerable"].BaseValue);
    }

    private static void ApplyWeakAndVulnerable(
        CardOnPlayMirrorContext context,
        Creature target,
        decimal weakAmount,
        decimal vulnerableAmount)
    {
        context.ApplyPower<WeakPower>(target, weakAmount);
        context.ApplyPower<VulnerablePower>(target, vulnerableAmount);
    }
}
