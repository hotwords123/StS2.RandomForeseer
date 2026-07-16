using MegaCrit.Sts2.Core.Commands;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.CardOnPlay;

internal static class CardOnPlayMirrorContextExtensions
{
    // Convenience extension method to simulate a single-targeted attack command.
    // Callers should ensure that the card play has a target before calling this method.
    public static void AttackSingle(this CardOnPlayMirrorContext context, int hitCount = 1)
    {
        if (context.CardPlay.Target is null)
        {
            throw new InvalidOperationException("Cannot simulate a targeted attack without a target.");
        }

        DamageCmd.Attack(context.PreviewCard.DynamicVars.Damage.BaseValue)
            .FromCard(context.PreviewCard, context.CardPlay)
            .WithHitCount(hitCount)
            .Targeting(context.CardPlay.Target)
            .Simulate(context.Simulator);
    }

    // Convenience extension method to simulate an attack command targeting all opponents.
    public static void AttackAllOpponents(this CardOnPlayMirrorContext context, int hitCount = 1)
    {
        DamageCmd.Attack(context.PreviewCard.DynamicVars.Damage.BaseValue)
            .FromCard(context.PreviewCard, context.CardPlay)
            .WithHitCount(hitCount)
            .TargetingAllOpponents(context.CombatState)
            .Simulate(context.Simulator);
    }

    // Convenience extension method to simulate a random-targeted attack command.
    public static void AttackRandomOpponents(this CardOnPlayMirrorContext context, int hitCount = 1)
    {
        DamageCmd.Attack(context.PreviewCard.DynamicVars.Damage.BaseValue)
            .FromCard(context.PreviewCard, context.CardPlay)
            .WithHitCount(hitCount)
            .TargetingRandomOpponents(context.CombatState)
            .Simulate(context.Simulator);
    }
}
