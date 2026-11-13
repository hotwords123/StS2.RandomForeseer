using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models.Cards;
using RandomForeseer.InCombat.Simulation;

namespace RandomForeseer.InCombat.Mirrors.Cards.OnPlay;

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

        context.AttackRandomOpponents(statuses.Count);
    }

    public static void StardustOnPlay(Stardust card, CardOnPlayMirrorContext context)
    {
        context.AttackRandomOpponents(context.ResolveStarXValue());
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

    public static void VolleyOnPlay(Volley card, CardOnPlayMirrorContext context)
    {
        context.AttackRandomOpponents(context.ResolveEnergyXValue());
    }
}
