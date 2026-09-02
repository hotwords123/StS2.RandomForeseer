using System.Diagnostics.CodeAnalysis;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.ValueProps;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Cards.OnPlay;

internal static class GeneralCardMirrors
{
    /// <summary>
    /// Mirrors the draw side effect of <see cref="ViciousPower"/> after this card applies Vulnerable.
    /// </summary>
    /// <remarks>
    /// Applying powers is otherwise outside the simulator's state domain. This deliberately mirrors only the
    /// immediately observable Vicious draw, using the live Vicious amount and without mutating either power.
    /// </remarks>
    public static void GeneralViciousDrawAfterVulnerableOnPlay(CardModel card, CardOnPlayMirrorContext context)
    {
        var drawPerApplication = card.Owner.Creature.GetPowerAmount<ViciousPower>();
        if (drawPerApplication <= 0)
        {
            return;
        }

        var applicationCount = card.TargetType switch
        {
            TargetType.AnyEnemy => context.State.GetCreature(context.Target).IsAlive ? 1 : 0,
            TargetType.AllEnemies => context.CombatState.GetOpponentsOf(card.Owner.Creature)
                .Count(target => context.State.GetCreature(target).IsAlive),
            _ => 0
        };

        if (applicationCount > 0)
        {
            context.Simulator.Draw(card.Owner, drawPerApplication * applicationCount);
        }
    }

    public static void GeneralAttackThenViciousDrawOnPlay(CardModel card, CardOnPlayMirrorContext context)
    {
        GeneralAttackOnPlay(card, context);
        GeneralViciousDrawAfterVulnerableOnPlay(card, context);
    }

    public static void GeneralBlockThenViciousDrawOnPlay(CardModel card, CardOnPlayMirrorContext context)
    {
        GeneralBlockOnPlay(card, context);
        GeneralViciousDrawAfterVulnerableOnPlay(card, context);
    }

    /// <summary>
    /// Simulates a general draw of one card for the card's owner.
    /// </summary>
    public static void GeneralOwnerDrawOneOnPlay(CardModel card, CardOnPlayMirrorContext context)
    {
        context.Simulator.Draw(card.Owner, 1);
    }

    /// <summary>
    /// Simulates an unconditional draw based on the card's <c>Cards</c> dynamic var for the card's owner.
    /// </summary>
    public static void GeneralOwnerDrawOnPlay(CardModel card, CardOnPlayMirrorContext context)
    {
        if (!card.DynamicVars.TryGetValue("Cards", out var cardsVar))
        {
            Entry.Logger.Warn($"Card {card.Id} has no cards var to simulate a draw.");
            context.History.RecordRisk(PredictionRiskReason.MethodMirrorIncomplete);
            return;
        }

        context.Simulator.Draw(card.Owner, context.Calculate(cardsVar));
    }

    /// <summary>
    /// Simulates a general attack when a card is played.
    /// </summary>
    /// <remarks>
    /// Targeting examples:
    /// <list type="bullet">
    /// <item><see cref="StrikeIronclad"/> targets any enemy.</item>
    /// <item><see cref="Breakthrough"/> targets all enemies.</item>
    /// <item><see cref="SwordBoomerang"/> targets random enemies.</item>
    /// </list>
    /// </remarks>
    public static void GeneralAttackOnPlay(CardModel card, CardOnPlayMirrorContext context)
    {
        if (!TryGetDynamicVar(card, ["CalculatedDamage", "Damage", "OstyDamage"], out var damage))
        {
            Entry.Logger.Warn($"Card {card.Id} has no damage var to simulate an attack command.");
            context.History.RecordRisk(PredictionRiskReason.MethodMirrorIncomplete);
            return;
        }

        var command = damage is CalculatedDamageVar calculatedDamageVar
            ? DamageCmd.Attack(calculatedDamageVar)
            : DamageCmd.Attack(context.Calculate(damage));

        if (card.Tags.Contains(CardTag.OstyAttack))
        {
            if (card.Owner.Osty is not { } osty || !context.State.GetCreature(osty).IsAlive)
            {
                return;
            }

            command.FromOsty(osty, card, context.CardPlay);
        }
        else
        {
            command.FromCard(card, context.CardPlay);
        }

        if (TryGetDynamicVar(card, ["Repeat", "CalculatedHits"], out var repeat))
        {
            command.WithHitCount((int)context.Calculate(repeat));
        }
        else if (card.EnergyCost.CostsX)
        {
            command.WithHitCount(context.ResolveEnergyXValue());
        }
        else if (card.HasStarCostX)
        {
            command.WithHitCount(context.ResolveStarXValue());
        }

        switch (card.TargetType)
        {
            case TargetType.AnyEnemy:
                command.Targeting(context.Target);
                break;

            case TargetType.AllEnemies:
                command.TargetingAllOpponents(context.CombatState);
                break;

            case TargetType.RandomEnemy:
                command.TargetingRandomOpponents(context.CombatState);
                break;

            default:
                Entry.Logger.Warn($"Attack {card.Id} has an unsupported target type: {card.TargetType}");
                context.History.RecordRisk(PredictionRiskReason.MethodMirrorIncomplete);
                return;
        }

        command.Simulate(context.Simulator);
    }

    /// <summary>
    /// Simulates a general block gain when a card is played.
    /// </summary>
    /// <remarks>
    /// Targeting examples:
    /// <list type="bullet">
    /// <item><see cref="DefendIronclad"/> targets self.</item>
    /// <item><see cref="Lift"/> targets any ally.</item>
    /// <item><see cref="Rally"/> targets all allies.</item>
    /// <item>
    /// <see cref="IronWave"/> is a combined attack-and-block card that targets an enemy while its block effect targets
    /// the owner. <see cref="Defy"/> is a debuff Skill that targets an enemy while its block effect targets the owner.
    /// </item>
    /// </list>
    /// </remarks>
    public static void GeneralBlockOnPlay(CardModel card, CardOnPlayMirrorContext context)
    {
        Action<Creature> blockAction;
        if (TryGetDynamicVar(card, ["CalculatedBlock", "Block"], out var block))
        {
            var amount = context.Calculate(block);
            var props = block switch
            {
                CalculatedBlockVar calculatedBlockVar => calculatedBlockVar.Props,
                BlockVar blockVar => blockVar.Props,
                _ => ValueProp.Move
            };
            blockAction = target => context.GainBlock(target, amount, props);
        }
        else
        {
            Entry.Logger.Warn($"Card {card.Id} has no block var to simulate a block gain.");
            context.History.RecordRisk(PredictionRiskReason.MethodMirrorIncomplete);
            return;
        }

        switch (card.TargetType)
        {
            case TargetType.Self:
            case TargetType.AnyEnemy or TargetType.AllEnemies or TargetType.RandomEnemy:
                blockAction(card.Owner.Creature);
                break;

            case TargetType.AnyAlly:
                blockAction(context.Target);
                break;

            case TargetType.AllAllies:
                var allies = context.CombatState.GetTeammatesOf(card.Owner.Creature)
                    .Where(creature => creature.IsPlayer && context.State.GetCreature(creature).IsAlive);
                foreach (var ally in allies)
                {
                    blockAction(ally);
                }
                break;

            default:
                Entry.Logger.Warn($"Block {card.Id} has an unsupported target type: {card.TargetType}");
                context.History.RecordRisk(PredictionRiskReason.MethodMirrorIncomplete);
                return;
        }
    }

    private static bool TryGetDynamicVar(
        CardModel card,
        IEnumerable<string> candidateKeys,
        [NotNullWhen(true)] out DynamicVar? dynamicVar)
    {
        foreach (var key in candidateKeys)
        {
            if (card.DynamicVars.TryGetValue(key, out dynamicVar))
            {
                return true;
            }
        }

        dynamicVar = null;
        return false;
    }
}
