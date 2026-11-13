using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.ValueProps;
using RandomForeseer.InCombat.Mirrors.Shared;
using RandomForeseer.InCombat.Simulation;

namespace RandomForeseer.InCombat.Mirrors.Cards.OnPlay;

internal static class SpecialAttackCardMirrors
{
    public static void TimberOnPlay(Timber card, CardOnPlayMirrorContext context)
    {
        var attack = DamageCmd.Attack(card.DynamicVars.Damage.BaseValue)
            .FromCard(card, context.CardPlay)
            .Targeting(context.Target);
        attack.Simulate(context.Simulator);

        if (attack.Results.SelectMany(results => results).Any(result => result.WasTargetKilled))
        {
            DamageCmd.Attack(card.DynamicVars["OnKillDamage"].BaseValue)
                .FromCard(card, context.CardPlay)
                .TargetingAllOpponents(context.CombatState)
                .Simulate(context.Simulator);
        }
    }

    public static void WahBlastOnPlay(WahBlast card, CardOnPlayMirrorContext context)
    {
        var hitsTwice = MelodyMirrors.PlayedThisTurn(context.Simulator, card.Owner, CardTag.Cho) ||
            MelodyMirrors.PlayedThisTurn(context.Simulator, card.Owner, CardTag.Ki);
        context.AttackAllOpponents(hitsTwice ? 2 : 1);
    }

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
            .Where(creature => creature != context.Target && context.State.GetCreature(creature).IsHittable)
            .ToArray();

        if (targets.Length == 0)
        {
            return;
        }

        attackContext.AddHit(context.Simulator.Damage(
            targets,
            firstResult.TotalDamage + firstResult.OverkillDamage,
            ValueProp.Move,
            card.Owner.Creature,
            context.Card,
            context.CardPlay,
            ModifyDamageHookType.Cap));
    }
}
