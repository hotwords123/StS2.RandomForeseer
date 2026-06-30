using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Random;
using RandomForeseer.RandomForeseerCode.Common;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.Models.Potions;

namespace RandomForeseer.RandomForeseerCode.InCombat;

internal static class CombatCardGenerationPrediction
{
    public static IReadOnlyList<IHoverTip> GetCardHoverTips(CardModel card)
    {
        if (!RandomForeseerSettings.IsPredictionFeatureEnabled(RandomForeseerSettings.EnableCombatCardPrediction))
        {
            return [];
        }

        return PredictionHoverTips.Cards(PredictCards(card));
    }

    public static IReadOnlyList<IHoverTip> GetPotionHoverTips(PotionModel potion)
    {
        if (!RandomForeseerSettings.IsPredictionFeatureEnabled(RandomForeseerSettings.EnablePotionCardPrediction) ||
            !ShouldShowPotionCardPrediction(potion))
        {
            return [];
        }

        return PredictionHoverTips.Cards(PredictCards(potion));
    }

    private static bool ShouldShowPotionCardPrediction(PotionModel potion)
    {
        return RandomForeseerSettings.IsFairPredictionAllowed(PredictionFairness.UnfairInAllModes) ||
            CombatManager.Instance.IsInProgress && !potion.Owner.Creature.IsDead;
    }

    private static IReadOnlyList<CardModel> PredictCards(CardModel card)
    {
        var owner = card.Owner;
        var previewRng = PredictionUtils.CloneRng(owner.RunState.Rng.CombatCardGeneration);

        return card switch
        {
            BundleOfJoy => PredictColorlessCards(owner, card.DynamicVars.Cards.IntValue, previewRng),
            Discovery => PredictCharacterCards(owner, null, 3, previewRng),
            Distraction => PredictCharacterCards(owner, CardType.Skill, 1, previewRng),
            InfernalBlade => PredictCharacterCards(owner, CardType.Attack, 1, previewRng),
            JackOfAllTrades => PredictJackOfAllTrades(card, previewRng),
            Jackpot => PredictJackpot(card, previewRng),
            Largesse => PredictLargesseCards(card, previewRng),
            MadScience madScience when madScience.TinkerTimeRider == TinkerTime.RiderEffect.Chaos =>
                PredictCharacterCards(owner, null, 1, previewRng),
            ManifestAuthority => PredictManifestAuthority(card, previewRng),
            Metamorphosis => PredictMetamorphosis(card, previewRng),
            Quasar => PredictQuasar(card, previewRng),
            Splash => PredictSplash(card, previewRng),
            Stoke => PredictStoke(card, previewRng),
            WhiteNoise => PredictCharacterCards(owner, CardType.Power, 1, previewRng),
            _ => []
        };
    }

    private static IReadOnlyList<CardModel> PredictCards(PotionModel potion)
    {
        var owner = potion.Owner;
        var previewRng = PredictionUtils.CloneRng(owner.RunState.Rng.CombatCardGeneration);

        return potion switch
        {
            AttackPotion => PredictCharacterCards(owner, CardType.Attack, 3, previewRng),
            SkillPotion => PredictCharacterCards(owner, CardType.Skill, 3, previewRng),
            PowerPotion => PredictCharacterCards(owner, CardType.Power, 3, previewRng),
            ColorlessPotion => PredictColorlessCards(owner, 3, previewRng),
            CosmicConcoction => PredictColorlessCards(owner, potion.DynamicVars.Cards.IntValue, previewRng)
                .Select(PredictionUtils.ToUpgradedCard)
                .ToList(),
            OrobicAcid => PredictOrobicAcid(potion, previewRng),
            _ => []
        };
    }

    private static IReadOnlyList<CardModel> PredictCharacterCards(
        Player player,
        CardType? type,
        int count,
        Rng previewRng)
    {
        var candidates = PredictionUtils.GetUnlockedCharacterCards(player);
        if (type is { } cardType)
        {
            candidates = candidates.Where(card => card.Type == cardType);
        }

        return TakeRandomDistinctForCombat(player, candidates, count, previewRng);
    }

    private static IReadOnlyList<CardModel> PredictColorlessCards(Player player, int count, Rng previewRng)
    {
        var candidates = PredictionUtils.GetUnlockedColorlessCards(player);

        return TakeRandomDistinctForCombat(player, candidates, count, previewRng);
    }

    private static IReadOnlyList<CardModel> PredictJackOfAllTrades(CardModel source, Rng previewRng)
    {
        var owner = source.Owner;
        var candidates = PredictionUtils.GetUnlockedColorlessCards(owner)
            .Where(card => card is not JackOfAllTrades);

        return TakeRandomDistinctForCombat(owner, candidates, source.DynamicVars.Cards.IntValue, previewRng);
    }

    private static IReadOnlyList<CardModel> PredictJackpot(CardModel source, Rng previewRng)
    {
        var owner = source.Owner;
        var candidates = PredictionUtils.GetUnlockedCharacterCards(owner)
            .Where(card => card.EnergyCost is { Canonical: 0, CostsX: false });
        var cards = TakeRandomForCombat(owner, candidates, source.DynamicVars.Cards.IntValue, previewRng);

        return PredictionUtils.ToUpgradedCardsIf(cards, source.IsUpgraded);
    }

    private static IReadOnlyList<CardModel> PredictManifestAuthority(CardModel source, Rng previewRng)
    {
        var cards = PredictColorlessCards(source.Owner, 1, previewRng);
        return PredictionUtils.ToUpgradedCardsIf(cards, source.IsUpgraded);
    }

    private static IReadOnlyList<CardModel> PredictMetamorphosis(CardModel source, Rng previewRng)
    {
        var owner = source.Owner;
        var candidates = PredictionUtils.GetUnlockedCharacterCards(owner)
            .Where(card => card.Type == CardType.Attack);

        return TakeRandomForCombat(owner, candidates, source.DynamicVars.Cards.IntValue, previewRng);
    }

    private static IReadOnlyList<CardModel> PredictQuasar(CardModel source, Rng previewRng)
    {
        var cards = PredictColorlessCards(source.Owner, 3, previewRng);
        return PredictionUtils.ToUpgradedCardsIf(cards, source.IsUpgraded);
    }

    private static IReadOnlyList<CardModel> PredictSplash(CardModel source, Rng previewRng)
    {
        var owner = source.Owner;
        var pools = owner.UnlockState.CharacterCardPools.ToList();
        if (pools.Count > 1)
        {
            pools.Remove(owner.Character.CardPool);
        }

        var candidates = pools
            .SelectMany(pool => PredictionUtils.GetUnlockedCards(owner, pool))
            .Where(card => card.Type == CardType.Attack);
        var cards = TakeRandomDistinctForCombat(owner, candidates, 3, previewRng);

        return PredictionUtils.ToUpgradedCardsIf(cards, source.IsUpgraded);
    }

    private static IReadOnlyList<CardModel> PredictStoke(CardModel source, Rng previewRng)
    {
        var owner = source.Owner;
        var cardsToExhaust = PileType.Hand.GetPile(owner).Cards.Count(card => card != source);
        if (cardsToExhaust <= 0)
        {
            return [];
        }

        var candidates = PredictionUtils.GetUnlockedCharacterCards(owner);
        var cards = TakeRandomForCombat(owner, candidates, cardsToExhaust, previewRng);

        return PredictionUtils.ToUpgradedCardsIf(cards, source.IsUpgraded);
    }

    private static IReadOnlyList<CardModel> PredictLargesseCards(CardModel source, Rng previewRng)
    {
        if (source.Owner.Creature.CombatState == null)
        {
            return [];
        }

        return GetLargesseTargets(source)
            .SelectMany(target => PredictLargesseForTarget(source, target.Player!, previewRng))
            .DistinctBy(card => card.Id)
            .ToList();
    }

    private static IEnumerable<Creature> GetLargesseTargets(CardModel source)
    {
        return source.Owner.Creature.CombatState?.PlayerCreatures
            .Where(creature => IsValidLargesseTarget(source, creature)) ?? [];
    }

    private static bool IsValidLargesseTarget(CardModel source, Creature? target)
    {
        return target is { IsHittable: true, IsPlayer: true } &&
            target != source.Owner.Creature &&
            source.IsValidTarget(target);
    }

    private static IReadOnlyList<CardModel> PredictLargesseForTarget(CardModel source, Player target, Rng sourceRng)
    {
        var cards = TakeRandomDistinctForCombat(
            target,
            PredictionUtils.GetUnlockedColorlessCards(target),
            1,
            PredictionUtils.CloneRng(sourceRng));

        return PredictionUtils.ToUpgradedCardsIf(cards, source.IsUpgraded);
    }

    private static IReadOnlyList<CardModel> PredictOrobicAcid(PotionModel potion, Rng previewRng)
    {
        var player = potion.Owner;
        var cards = new List<CardModel>();
        cards.AddRange(PredictCharacterCards(player, CardType.Attack, 1, previewRng));
        cards.AddRange(PredictCharacterCards(player, CardType.Skill, 1, previewRng));
        cards.AddRange(PredictCharacterCards(player, CardType.Power, 1, previewRng));
        return cards;
    }

    private static IReadOnlyList<CardModel> TakeRandomDistinctForCombat(
        Player player,
        IEnumerable<CardModel> cards,
        int count,
        Rng rng)
    {
        return FilterForCombatAndPlayerCount(player, cards)
            .ToList()
            .UnstableShuffle(rng)
            .Take(count)
            .ToList();
    }

    private static IReadOnlyList<CardModel> TakeRandomForCombat(
        Player player,
        IEnumerable<CardModel> cards,
        int count,
        Rng rng)
    {
        var options = FilterForCombatAndPlayerCount(player, cards).ToList();
        if (options.Count == 0)
        {
            return [];
        }

        var results = new List<CardModel>();
        for (var i = 0; i < count; i++)
        {
            var card = rng.NextItem(options);
            if (card != null)
            {
                results.Add(card);
            }
        }

        return results;
    }

    private static IEnumerable<CardModel> FilterForCombatAndPlayerCount(Player player, IEnumerable<CardModel> cards)
    {
        return CardFactory.FilterForPlayerCount(player.RunState, CardFactory.FilterForCombat(cards));
    }
}
