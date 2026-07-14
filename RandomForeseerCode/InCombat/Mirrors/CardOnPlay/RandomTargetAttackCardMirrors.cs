using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.CardOnPlay;

internal static class RandomTargetAttackCardMirrors
{
    public static void FlakCannonOnPlay(FlakCannon card, CardOnPlayMirrorContext context)
    {
        var statuses = context.OwnerState.AllCards
            .Where(predictedCard =>
                predictedCard.Preview.Type is CardType.Status &&
                !context.OwnerState.ExhaustPile.Cards.Contains(predictedCard))
            .ToList();

        foreach (var status in statuses)
        {
            context.Simulator.Exhaust(status);
        }

        SimulateRandomAttack(card, context, statuses.Count);
    }

    public static void RicochetOnPlay(Ricochet card, CardOnPlayMirrorContext context)
    {
        SimulateRandomAttack(card, context, card.DynamicVars.Repeat.IntValue);
    }

    public static void RipAndTearOnPlay(RipAndTear card, CardOnPlayMirrorContext context)
    {
        SimulateRandomAttack(card, context, 2);
    }

    public static void StardustOnPlay(Stardust card, CardOnPlayMirrorContext context)
    {
        SimulateRandomAttack(card, context, context.Card.ResolveStarXValue(context.State));
    }

    public static void SweepingGazeOnPlay(SweepingGaze card, CardOnPlayMirrorContext context)
    {
        if (card.Owner.Osty is { } osty && context.State.GetCreature(osty).IsAlive)
        {
            DamageCmd.Attack(card.DynamicVars.OstyDamage.BaseValue)
                .FromOsty(osty, card, context.CardPlay)
                .TargetingRandomOpponents(context.CombatState)
                .Simulate(context.Simulator);
        }
    }

    public static void SwordBoomerangOnPlay(SwordBoomerang card, CardOnPlayMirrorContext context)
    {
        SimulateRandomAttack(card, context, card.DynamicVars.Repeat.IntValue);
    }

    public static void VolleyOnPlay(Volley card, CardOnPlayMirrorContext context)
    {
        SimulateRandomAttack(card, context, context.Card.ResolveEnergyXValue(context.State));
    }

    private static void SimulateRandomAttack(
        CardModel card,
        CardOnPlayMirrorContext context,
        int hitCount)
    {
        DamageCmd.Attack(card.DynamicVars.Damage.BaseValue)
            .FromCard(card, context.CardPlay)
            .WithHitCount(hitCount)
            .TargetingRandomOpponents(context.CombatState)
            .Simulate(context.Simulator);
    }
}
