using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Events;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.CardOnPlay;

internal static class CardGenerationCardMirrors
{
    public static void BundleOfJoyOnPlay(BundleOfJoy card, CardOnPlayMirrorContext context)
    {
        var cards = card.Owner.GetUnlockedColorlessCards()
            .GetDistinctForCombat(
                card.Owner,
                card.DynamicVars.Cards.IntValue,
                context.Rng.CombatCardGeneration)
            .ToList();

        context.Simulator.AddGeneratedCardsToCombat(cards, PileType.Hand, card.Owner);
    }

    public static void DistractionOnPlay(Distraction card, CardOnPlayMirrorContext context)
    {
        var cards = card.Owner.GetUnlockedCharacterCards()
            .Where(candidate => candidate.Type == CardType.Skill)
            .GetDistinctForCombat(card.Owner, 1, context.Rng.CombatCardGeneration)
            .Select(generatedCard => generatedCard.SetToFreeThisTurn())
            .ToList();

        context.Simulator.AddGeneratedCardsToCombat(cards, PileType.Hand, card.Owner);
    }

    public static void DiscoveryOnPlay(Discovery card, CardOnPlayMirrorContext context)
    {
        var cards = card.Owner.GetUnlockedCharacterCards()
            .GetDistinctForCombat(card.Owner, 3, context.Rng.CombatCardGeneration)
            .ToList();

        RecordOptions(context, cards);
    }

    public static void InfernalBladeOnPlay(InfernalBlade card, CardOnPlayMirrorContext context)
    {
        var cards = card.Owner.GetUnlockedCharacterCards()
            .Where(candidate => candidate.Type == CardType.Attack)
            .GetDistinctForCombat(card.Owner, 1, context.Rng.CombatCardGeneration)
            .Select(generatedCard => generatedCard.SetToFreeThisTurn())
            .ToList();

        context.Simulator.AddGeneratedCardsToCombat(cards, PileType.Hand, card.Owner);
    }

    public static void JackOfAllTradesOnPlay(JackOfAllTrades card, CardOnPlayMirrorContext context)
    {
        var cards = card.Owner.GetUnlockedColorlessCards()
            .Where(candidate => candidate is not JackOfAllTrades)
            .GetDistinctForCombat(
                card.Owner,
                card.DynamicVars.Cards.IntValue,
                context.Rng.CombatCardGeneration)
            .ToList();

        context.Simulator.AddGeneratedCardsToCombat(cards, PileType.Hand, card.Owner);
    }

    public static void JackpotOnPlay(Jackpot card, CardOnPlayMirrorContext context)
    {
        context.AttackSingle();

        var cards = card.Owner.GetUnlockedCharacterCards()
            .Where(candidate => candidate.EnergyCost is { Canonical: 0, CostsX: false })
            .GetForCombat(
                card.Owner,
                card.DynamicVars.Cards.IntValue,
                context.Rng.CombatCardGeneration)
            .UpgradeIf(card.IsUpgraded)
            .ToList();

        context.Simulator.AddGeneratedCardsToCombat(cards, PileType.Hand, card.Owner);
    }

    public static void LargesseOnPlay(Largesse card, CardOnPlayMirrorContext context)
    {
        var targetPlayer = context.CardPlay.Target?.Player
            ?? throw new InvalidOperationException("Largesse must have a target player");

        var cards = targetPlayer.GetUnlockedColorlessCards()
            .GetDistinctForCombat(targetPlayer, 1, context.Rng.CombatCardGeneration)
            .UpgradeIf(card.IsUpgraded)
            .ToList();

        context.Simulator.AddGeneratedCardsToCombat(cards, PileType.Hand, card.Owner);
    }

    public static void MadScienceOnPlay(MadScience card, CardOnPlayMirrorContext context)
    {
        if (card is not { TinkerTimeType: CardType.Skill, TinkerTimeRider: TinkerTime.RiderEffect.Chaos })
        {
            context.History.RecordRisk(PredictionRiskReason.MethodMirrorIncomplete);
            return;
        }

        context.Simulator.GainBlock(card.Owner.Creature, card.DynamicVars.Block, context.Card);

        var cards = card.Owner.GetUnlockedCharacterCards()
            .GetDistinctForCombat(card.Owner, 1, context.Rng.CombatCardGeneration)
            .Select(generatedCard => generatedCard.SetToFreeThisTurn())
            .ToList();

        context.Simulator.AddGeneratedCardsToCombat(cards, PileType.Hand, card.Owner);
    }

    public static void ManifestAuthorityOnPlay(ManifestAuthority card, CardOnPlayMirrorContext context)
    {
        context.Simulator.GainBlock(card.Owner.Creature, card.DynamicVars.Block, context.Card);

        var cards = card.Owner.GetUnlockedColorlessCards()
            .GetDistinctForCombat(card.Owner, 1, context.Rng.CombatCardGeneration)
            .UpgradeIf(card.IsUpgraded)
            .ToList();

        context.Simulator.AddGeneratedCardsToCombat(cards, PileType.Hand, card.Owner);
    }

    public static void MetamorphosisOnPlay(Metamorphosis card, CardOnPlayMirrorContext context)
    {
        var cards = card.Owner.GetUnlockedCharacterCards()
            .Where(candidate => candidate.Type == CardType.Attack)
            .GetForCombat(
                card.Owner,
                card.DynamicVars.Cards.IntValue,
                context.Rng.CombatCardGeneration)
            .Select(generatedCard => generatedCard.SetToFreeThisCombat())
            .ToList();

        context.Simulator.AddGeneratedCardsToCombat(
            cards,
            PileType.Draw,
            card.Owner,
            CardPilePosition.Random);
    }

    public static void QuasarOnPlay(Quasar card, CardOnPlayMirrorContext context)
    {
        var cards = card.Owner.GetUnlockedColorlessCards()
            .GetDistinctForCombat(
                card.Owner,
                3,
                context.Rng.CombatCardGeneration)
            .UpgradeIf(card.IsUpgraded)
            .ToList();

        RecordOptions(context, cards);
    }

    public static void SplashOnPlay(Splash card, CardOnPlayMirrorContext context)
    {
        var pools = card.Owner.UnlockState.CharacterCardPools.ToList();
        if (pools.Count > 1)
        {
            pools.Remove(card.Owner.Character.CardPool);
        }

        var cards = pools
            .SelectMany(card.Owner.GetUnlockedCards)
            .Where(candidate => candidate.Type == CardType.Attack)
            .GetDistinctForCombat(card.Owner, 3, context.Rng.CombatCardGeneration)
            .UpgradeIf(card.IsUpgraded)
            .ToList();

        RecordOptions(context, cards);
    }

    public static void StokeOnPlay(Stoke card, CardOnPlayMirrorContext context)
    {
        var cardsToExhaust = context.OwnerState.Hand.Cards.ToList();
        foreach (var cardToExhaust in cardsToExhaust)
        {
            context.Simulator.Exhaust(cardToExhaust);
        }

        var cards = card.Owner.GetUnlockedCharacterCards()
            .GetForCombat(card.Owner, cardsToExhaust.Count, context.Rng.CombatCardGeneration)
            .UpgradeIf(card.IsUpgraded)
            .ToList();

        context.Simulator.AddGeneratedCardsToCombat(cards, PileType.Hand, card.Owner);
    }

    public static void WhiteNoiseOnPlay(WhiteNoise card, CardOnPlayMirrorContext context)
    {
        var cards = card.Owner.GetUnlockedCharacterCards()
            .Where(candidate => candidate.Type == CardType.Power)
            .GetDistinctForCombat(card.Owner, 1, context.Rng.CombatCardGeneration)
            .Select(generatedCard => generatedCard.SetToFreeThisTurn())
            .ToList();

        context.Simulator.AddGeneratedCardsToCombat(cards, PileType.Hand, card.Owner);
    }

    private static void RecordOptions(CardOnPlayMirrorContext context, IReadOnlyList<PredictedCard> cards)
    {
        if (cards.Count == 0)
        {
            return;
        }

        context.Simulator.History.CardGenerationOptions(cards);
        // Vanilla next asks the player to choose an option. Record the deterministic options first,
        // then mark the unresolved choice so replayed or nested results inherit the uncertainty.
        context.History.RecordRisk(PredictionRiskReason.UnresolvedPlayerChoice);
    }
}
