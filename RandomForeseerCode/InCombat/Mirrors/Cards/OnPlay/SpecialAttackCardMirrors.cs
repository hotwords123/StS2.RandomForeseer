using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.ValueProps;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Cards.OnPlay;

internal static class SpecialAttackCardMirrors
{
    public static void EchoingSlashOnPlay(EchoingSlash card, CardOnPlayMirrorContext context)
    {
        using var attackContext = context.Simulator.CreateAttackContext(context.CardPlay);

        for (var hitCount = 1; hitCount > 0; hitCount--)
        {
            var results = context.Simulator.Damage(
                context.State.HittableEnemies,
                card.DynamicVars.Damage.BaseValue,
                card.DynamicVars.Damage.Props,
                card.Owner.Creature,
                context.Card,
                context.CardPlay);
            attackContext.AddHit(results);

            hitCount += results.Count(result => result.WasTargetKilled);
        }
    }

    public static void OmnisliceOnPlay(Omnislice card, CardOnPlayMirrorContext context)
    {
        using var attackContext = context.Simulator.CreateAttackContext(context.CardPlay);

        var initialResults = context.Simulator.Damage(
            [context.Target],
            card.DynamicVars.Damage.BaseValue,
            DamageProps.card,
            card.Owner.Creature,
            context.Card,
            context.CardPlay);
        attackContext.AddHit(initialResults);

        if (initialResults is not [var firstResult, ..])
        {
            return;
        }

        var targets = context.State.GetTeammatesOf(firstResult.Receiver)
            .Where(creature => creature != firstResult.Receiver && context.State.GetCreature(creature).IsHittable)
            .ToArray();

        if (targets.Length == 0)
        {
            return;
        }

        attackContext.AddHit(context.Simulator.Damage(
            targets,
            firstResult.TotalDamage + firstResult.OverkillDamage,
            DamageProps.cardUnpowered,
            card.Owner.Creature,
            context.Card,
            context.CardPlay));
    }
}
