using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Cards.OnPlay;

internal static class MultiHitAttackCardMirrors
{
    public static void AstralPulseOnPlay(AstralPulse card, CardOnPlayMirrorContext context)
    {
        context.AttackAllOpponents(hitCount: 2);
    }

    public static void BarrageOnPlay(Barrage card, CardOnPlayMirrorContext context)
    {
        context.AttackSingle(context.OwnerState.OrbQueue.Orbs.Count);
    }

    public static void DaggerSprayOnPlay(DaggerSpray card, CardOnPlayMirrorContext context)
    {
        context.AttackAllOpponents(hitCount: 2);
    }

    public static void DismantleOnPlay(Dismantle card, CardOnPlayMirrorContext context)
    {
        context.AttackSingle(context.Target.HasPower<VulnerablePower>() ? 2 : 1);
    }

    public static void FiendFireOnPlay(FiendFire card, CardOnPlayMirrorContext context)
    {
        var cardsToExhaust = context.OwnerState.Hand.Cards.ToArray();
        foreach (var cardToExhaust in cardsToExhaust)
        {
            context.Simulator.Exhaust(cardToExhaust);
        }

        context.AttackSingle(cardsToExhaust.Length);
    }

    public static void FinisherOnPlay(Finisher card, CardOnPlayMirrorContext context)
    {
        var hitCount = CombatManager.Instance.History.CardPlaysFinished.Count(entry =>
            entry.HappenedThisTurn(context.CombatState) &&
            entry.CardPlay.Card.Type == CardType.Attack &&
            entry.CardPlay.Player == card.Owner);
        hitCount += context.History.OfType<CombatPredictionCardPlayFinishedEntry>().Count(entry =>
            entry.CardPlay.Card.Type == CardType.Attack &&
            entry.CardPlay.Player == card.Owner);

        context.AttackSingle(hitCount);
    }

    public static void FlechettesOnPlay(Flechettes card, CardOnPlayMirrorContext context)
    {
        var hitCount = context.OwnerState.Hand.Cards.Count(predicted => predicted.Preview.Type == CardType.Skill);
        context.AttackSingle(hitCount);
    }

    public static void GunkUpOnPlay(GunkUp card, CardOnPlayMirrorContext context)
    {
        context.AttackSingle(card.DynamicVars.Repeat.IntValue);
        context.Simulator.CreateAndAddGeneratedCardsToCombat<Slimed>(
            card.Owner,
            PileType.Discard,
            count: 1,
            creator: card.Owner);
    }

    public static void HeavenlyDrillOnPlay(HeavenlyDrill card, CardOnPlayMirrorContext context)
    {
        var hitCount = context.ResolveEnergyXValue();
        if (hitCount >= card.DynamicVars.Energy.IntValue)
        {
            hitCount *= 2;
        }

        context.AttackSingle(hitCount);
    }

    public static void HelixDrillOnPlay(HelixDrill card, CardOnPlayMirrorContext context)
    {
        var hitCount = CombatManager.Instance.History.Entries
            .OfType<EnergySpentEntry>()
            .Where(entry => entry.HappenedThisTurn(context.CombatState) && entry.Actor.Player == card.Owner)
            .Sum(entry => entry.Amount);
        hitCount += context.History.OfType<CombatPredictionEnergySpentEntry>()
            .Where(entry => entry.Player == card.Owner)
            .Sum(entry => entry.Amount);
        // Vanilla HelixDrill subtracts EnergyCost.GetWithModifiers(CostModifiers.All) here.
        // That implementation is incorrect when dynamic cost-changing powers are active and
        // can disagree with the card description in rare cases. The mirror uses the resolved
        // resource value from CardPlay instead of reproducing that vanilla bug.
        hitCount -= context.CardPlay.Resources.EnergyValue;

        context.AttackSingle(Math.Max(0, hitCount));
    }

    public static void LunarBlastOnPlay(LunarBlast card, CardOnPlayMirrorContext context)
    {
        var hitCount = CombatManager.Instance.History.CardPlaysFinished.Count(entry =>
            entry.HappenedThisTurn(context.CombatState) &&
            entry.CardPlay.Card.Type == CardType.Skill &&
            entry.CardPlay.Player == card.Owner);
        hitCount += context.History.OfType<CombatPredictionCardPlayFinishedEntry>().Count(entry =>
            entry.CardPlay.Card.Type == CardType.Skill &&
            entry.CardPlay.Player == card.Owner);

        context.AttackSingle(hitCount);
    }

    public static void MaulOnPlay(Maul card, CardOnPlayMirrorContext context)
    {
        context.AttackSingle(hitCount: 2);

        var increase = card.DynamicVars["Increase"].BaseValue;
        foreach (var maul in context.OwnerState.AllCards.Where(predicted => predicted.Preview is Maul))
        {
            var preview = (Maul)maul.MutablePreview;
            preview.BuffFromMaulPlay(increase);
        }
    }

    public static void PullFromBelowOnPlay(PullFromBelow card, CardOnPlayMirrorContext context)
    {
        var hitCount = CombatManager.Instance.History.CardPlaysFinished.Count(entry =>
            entry.CardPlay.Player == card.Owner && entry.WasEthereal);
        hitCount += context.History.OfType<CombatPredictionCardPlayFinishedEntry>().Count(entry =>
            entry.CardPlay.Player == card.Owner && entry.WasEthereal);

        context.AttackSingle(hitCount);
    }

    public static void RadiateOnPlay(Radiate card, CardOnPlayMirrorContext context)
    {
        var hitCount = CombatManager.Instance.History.Entries
            .OfType<StarsModifiedEntry>()
            .Where(entry =>
                entry.HappenedThisTurn(context.CombatState) &&
                entry.Amount > 0 &&
                entry.Actor == card.Owner.Creature)
            .Sum(entry => entry.Amount);
        hitCount += context.History.OfType<CombatPredictionStarsModifiedEntry>()
            .Where(entry => entry.Player == card.Owner && entry.Amount > 0)
            .Sum(entry => entry.Amount);

        context.AttackAllOpponents(hitCount);
    }

    public static void RattleOnPlay(Rattle card, CardOnPlayMirrorContext context)
    {
        if (card.Owner.Osty is not { } osty || context.State.GetCreature(osty).IsDead)
        {
            return;
        }

        var hitCount = 1 + CombatManager.Instance.History.Entries
            .OfType<CreatureAttackedEntry>()
            .Count(entry => entry.HappenedThisTurn(context.CombatState) && entry.Actor == osty);
        hitCount += context.History.OfType<CombatPredictionCreatureAttackedEntry>()
            .Count(entry => entry.Attacker == osty);

        DamageCmd.Attack(card.DynamicVars.OstyDamage.BaseValue)
            .FromOsty(osty, card, context.CardPlay)
            .WithHitCount(hitCount)
            .Targeting(context.Target)
            .Simulate(context.Simulator);
    }

    public static void SovereignBladeOnPlay(SovereignBlade card, CardOnPlayMirrorContext context)
    {
        var hitCount = card.DynamicVars.Repeat.IntValue;
        if (card.Owner.Creature.HasPower<SeekingEdgePower>())
        {
            context.AttackAllOpponents(hitCount);
        }
        else
        {
            context.AttackSingle(hitCount);
        }

        var parryAmount = card.Owner.Creature.GetPowerAmount<ParryPower>();
        if (parryAmount > 0)
        {
            context.GainBlock(card.Owner.Creature, parryAmount, card.DynamicVars.CalculatedBlock.Props);
        }
    }

    public static void SpiteOnPlay(Spite card, CardOnPlayMirrorContext context)
    {
        var lostHpThisTurn = CombatManager.Instance.History.Entries
            .OfType<DamageReceivedEntry>()
            .Any(entry =>
                entry.HappenedThisTurn(context.CombatState) &&
                entry.Receiver == card.Owner.Creature &&
                entry.Result.UnblockedDamage > 0);
        lostHpThisTurn = lostHpThisTurn || context.History
            .OfType<CombatPredictionDamageReceivedEntry>()
            .Any(entry =>
                entry.Receiver == card.Owner.Creature &&
                entry.Result.UnblockedDamage > 0);

        context.AttackSingle(lostHpThisTurn ? card.DynamicVars.Repeat.IntValue : 1);
    }

    public static void TearAsunderOnPlay(TearAsunder card, CardOnPlayMirrorContext context)
    {
        var hitCount = 1 + CombatManager.Instance.History.Entries
            .OfType<DamageReceivedEntry>()
            .Count(entry =>
                entry.Receiver == card.Owner.Creature &&
                entry.Result.UnblockedDamage > 0);
        hitCount += context.History
            .OfType<CombatPredictionDamageReceivedEntry>()
            .Count(entry =>
                entry.Receiver == card.Owner.Creature &&
                entry.Result.UnblockedDamage > 0);

        context.AttackSingle(hitCount);
    }

    public static void TwinStrikeOnPlay(TwinStrike card, CardOnPlayMirrorContext context)
    {
        context.AttackSingle(hitCount: 2);
    }
}
