using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models.Cards;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Cards.OnPlay;

internal static class CardDrawCardMirrors
{
    public static void CompileDriverOnPlay(CompileDriver card, CardOnPlayMirrorContext context)
    {
        context.AttackSingle();
        var drawCount = context.OwnerState.OrbQueue.Orbs.Select(orb => orb.Id).Distinct().Count();
        context.Simulator.Draw(card.Owner, drawCount);
    }

    public static void CalculatedGambleOnPlay(CalculatedGamble card, CardOnPlayMirrorContext context)
    {
        var cards = context.OwnerState.Hand.Cards.ToArray();
        context.Simulator.DiscardAndDraw(cards, cards.Length);
    }

    public static void ConstellationOnPlay(Constellation card, CardOnPlayMirrorContext context)
    {
        var player = context.TargetPlayer;
        context.Simulator.Draw(player, card.DynamicVars.Cards.BaseValue);
        context.Simulator.GainEnergy(player, card.DynamicVars.Energy.IntValue);
        context.GainBlock(player.Creature);
    }

    public static void EscapePlanOnPlay(EscapePlan card, CardOnPlayMirrorContext context)
    {
        var drawnCards = context.Simulator.Draw(card.Owner, 1);
        if (drawnCards is [{ Preview.Type: CardType.Skill }])
        {
            context.GainBlock(card.Owner.Creature);
        }
    }

    public static void ExpertiseOnPlay(Expertise card, CardOnPlayMirrorContext context)
    {
        var drawnCards = context.Simulator.Draw(card.Owner, card.DynamicVars.Cards.IntValue);
        foreach (var drawnCard in drawnCards)
        {
            drawnCard.MutablePreview.GiveSingleTurnRetain();
        }
    }

    public static void FetchOnPlay(Fetch card, CardOnPlayMirrorContext context)
    {
        if (card.Owner.Osty is not { } osty || context.State.GetCreature(osty).IsDead)
        {
            return;
        }

        DamageCmd.Attack(card.DynamicVars.OstyDamage.BaseValue)
            .FromOsty(card.Owner.Osty, card, context.CardPlay)
            .Targeting(context.Target)
            .Simulate(context.Simulator);

        if (!HasCardPlayFinishedThisTurn(context.Simulator, context.Card))
        {
            context.Simulator.Draw(card.Owner, card.DynamicVars.Cards.BaseValue);
        }
    }

    public static void FtlOnPlay(Ftl card, CardOnPlayMirrorContext context)
    {
        context.AttackSingle();

        if (CountCardPlaysFinishedThisTurn(context.Simulator, card.Owner) < card.DynamicVars[Ftl._playMaxKey].IntValue)
        {
            context.Simulator.Draw(card.Owner, card.DynamicVars.Cards.BaseValue);
        }
    }

    public static void HuddleUpOnPlay(HuddleUp card, CardOnPlayMirrorContext context)
    {
        var allies = context.State.GetTeammatesOf(card.Owner.Creature)
            .Where(creature => creature.IsPlayer && context.State.GetCreature(creature).IsAlive);
        foreach (var ally in allies)
        {
            context.Simulator.Draw(ally.Player!, card.DynamicVars.Cards.BaseValue);
        }
    }

    public static void ImpatienceOnPlay(Impatience card, CardOnPlayMirrorContext context)
    {
        if (context.OwnerState.Hand.Cards.All(predicted => predicted.Preview.Type != CardType.Attack))
        {
            context.Simulator.Draw(card.Owner, card.DynamicVars.Cards.BaseValue);
        }
    }

    public static void PillageOnPlay(Pillage card, CardOnPlayMirrorContext context)
    {
        context.AttackSingle();

        while (true)
        {
            var drawnCards = context.Simulator.Draw(card.Owner, 1);
            if (drawnCards is not [{ Preview.Type: CardType.Attack }] ||
                context.OwnerState.Hand.Cards.Count >= CardPile.MaxCardsInHand)
            {
                break;
            }
        }
    }

    public static void RebootOnPlay(Reboot card, CardOnPlayMirrorContext context)
    {
        context.Simulator.MoveHandToDrawPile(card.Owner);
        context.Simulator.Shuffle(card.Owner);
        context.Simulator.Draw(card.Owner, card.DynamicVars.Cards.BaseValue);
    }

    public static void RestlessnessOnPlay(Restlessness card, CardOnPlayMirrorContext context)
    {
        if (context.OwnerState.Hand.IsEmpty)
        {
            context.Simulator.Draw(card.Owner, card.DynamicVars.Cards.IntValue);
            context.Simulator.GainEnergy(card.Owner, card.DynamicVars.Energy.IntValue);
        }
    }

    public static void ScrapeOnPlay(Scrape card, CardOnPlayMirrorContext context)
    {
        context.AttackSingle();

        var cardsToDiscard = context.Simulator.Draw(card.Owner, card.DynamicVars.Cards.IntValue)
            .Where(drawnCard =>
                drawnCard.Preview.EnergyCost.CostsX ||
                drawnCard.GetEnergyCostWithModifiers(context.Simulator) != 0)
            .ToList();
        context.Simulator.Discard(cardsToDiscard);
    }

    public static void ScrawlOnPlay(Scrawl card, CardOnPlayMirrorContext context)
    {
        var count = CardPile.MaxCardsInHand - context.OwnerState.Hand.Cards.Count;
        context.Simulator.Draw(card.Owner, count);
    }

    private static int CountCardPlaysFinishedThisTurn(CombatPredictionSimulator simulator, Player player)
    {
        var count = CombatManager.Instance.History.CardPlaysFinished
            .Count(entry =>
                entry.HappenedThisTurn(simulator.State.CombatState) &&
                entry.CardPlay.Player == player);
        count += simulator.History.OfType<CombatPredictionCardPlayFinishedEntry>()
            .Count(entry => entry.CardPlay.Player == player);
        return count;
    }

    private static bool HasCardPlayFinishedThisTurn(CombatPredictionSimulator simulator, PredictedCard card)
    {
        var flag = CombatManager.Instance.History.CardPlaysFinished
            .Any(entry =>
                entry.HappenedThisTurn(simulator.State.CombatState) &&
                entry.CardPlay.Card == card.Original);
        flag = flag || simulator.History
            .OfType<CombatPredictionCardPlayFinishedEntry>()
            .Any(entry => entry.Card.Original == card.Original);
        return flag;
    }
}
