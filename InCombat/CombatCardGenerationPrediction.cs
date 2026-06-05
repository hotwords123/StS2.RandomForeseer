using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Random;
using RandomForeseer.Common;

namespace RandomForeseer.InCombat;

internal static class CombatCardGenerationPrediction
{
    public static IReadOnlyList<IHoverTip> GetHoverTips(CardModel card)
    {
        if (!RandomForeseerSettings.IsPredictionFeatureEnabled(RandomForeseerSettings.EnableCombatCardPrediction) ||
            !card.IsMutable ||
            card.Pile?.Type != PileType.Hand ||
            card.Owner.Creature.CombatState == null)
        {
            return [];
        }

        var previewRng = PredictionUtils.CloneRng(card.Owner.RunState.Rng.CombatCardGeneration);
        var cards = PredictCards(card, previewRng);
        return PredictionHoverTips.Cards(cards);
    }

    private static IReadOnlyList<CardModel> PredictCards(CardModel card, Rng previewRng)
    {
        return card switch
        {
            BundleOfJoy => PredictColorlessCards(card, card.DynamicVars.Cards.IntValue, previewRng),
            Discovery => PredictCharacterCards(card, null, 3, previewRng),
            Distraction => PredictCharacterCards(card, CardType.Skill, 1, previewRng),
            InfernalBlade => PredictCharacterCards(card, CardType.Attack, 1, previewRng),
            JackOfAllTrades => PredictJackOfAllTrades(card, previewRng),
            Jackpot => PredictJackpot(card, previewRng),
            Largesse => PredictLargesseCards(card, previewRng),
            MadScience madScience when madScience.TinkerTimeRider == TinkerTime.RiderEffect.Chaos =>
                PredictCharacterCards(card, null, 1, previewRng),
            ManifestAuthority => PredictManifestAuthority(card, previewRng),
            Metamorphosis => PredictMetamorphosis(card, previewRng),
            Quasar => PredictQuasar(card, previewRng),
            Splash => PredictSplash(card, previewRng),
            Stoke => PredictStoke(card, previewRng),
            WhiteNoise => PredictCharacterCards(card, CardType.Power, 1, previewRng),
            _ => []
        };
    }

    private static IReadOnlyList<CardModel> PredictCharacterCards(
        CardModel source,
        CardType? type,
        int count,
        Rng previewRng)
    {
        return PredictionUtils.TakeRandomDistinctCharacterCardsForCombat(source.Owner, type, count, previewRng);
    }

    private static IReadOnlyList<CardModel> PredictColorlessCards(CardModel source, int count, Rng previewRng)
    {
        return PredictionUtils.TakeRandomDistinctColorlessCardsForCombat(source.Owner, count, previewRng);
    }

    private static IReadOnlyList<CardModel> PredictJackOfAllTrades(CardModel source, Rng previewRng)
    {
        var owner = source.Owner;
        var candidates = PredictionUtils.GetUnlockedColorlessCards(owner)
            .Where(card => card is not JackOfAllTrades);

        return PredictionUtils.TakeRandomDistinctForCombat(owner, candidates, source.DynamicVars.Cards.IntValue, previewRng);
    }

    private static IReadOnlyList<CardModel> PredictJackpot(CardModel source, Rng previewRng)
    {
        var owner = source.Owner;
        var candidates = PredictionUtils.GetUnlockedCharacterCards(owner)
            .Where(card => card.EnergyCost is { Canonical: 0, CostsX: false });
        var cards = PredictionUtils.TakeRandomForCombat(owner, candidates, source.DynamicVars.Cards.IntValue, previewRng);

        return source.IsUpgraded
            ? cards.Select(PredictionUtils.ToUpgradedPreviewCard).ToList()
            : cards;
    }

    private static IReadOnlyList<CardModel> PredictManifestAuthority(CardModel source, Rng previewRng)
    {
        var cards = PredictColorlessCards(source, 1, previewRng);
        return source.IsUpgraded
            ? cards.Select(PredictionUtils.ToUpgradedPreviewCard).ToList()
            : cards;
    }

    private static IReadOnlyList<CardModel> PredictMetamorphosis(CardModel source, Rng previewRng)
    {
        var owner = source.Owner;
        var candidates = PredictionUtils.GetUnlockedCharacterCards(owner)
            .Where(card => card.Type == CardType.Attack);

        return PredictionUtils.TakeRandomForCombat(owner, candidates, source.DynamicVars.Cards.IntValue, previewRng);
    }

    private static IReadOnlyList<CardModel> PredictQuasar(CardModel source, Rng previewRng)
    {
        var cards = PredictColorlessCards(source, 3, previewRng);
        return source.IsUpgraded
            ? cards.Select(PredictionUtils.ToUpgradedPreviewCard).ToList()
            : cards;
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
            .SelectMany(pool => pool.GetUnlockedCards(owner.UnlockState, owner.RunState.CardMultiplayerConstraint))
            .Where(card => card.Type == CardType.Attack);
        var cards = PredictionUtils.TakeRandomDistinctForCombat(owner, candidates, 3, previewRng);

        return source.IsUpgraded
            ? cards.Select(PredictionUtils.ToUpgradedPreviewCard).ToList()
            : cards;
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
        var cards = PredictionUtils.TakeRandomForCombat(owner, candidates, cardsToExhaust, previewRng);

        return source.IsUpgraded
            ? cards.Select(PredictionUtils.ToUpgradedPreviewCard).ToList()
            : cards;
    }

    private static IReadOnlyList<CardModel> PredictLargesseCards(CardModel source, Rng previewRng)
    {
        if (source.CombatState == null)
        {
            return [];
        }

        return GetLargesseTargets(source)
            .Select(target => PredictLargesseCard(source, target.Player!, previewRng))
            .OfType<CardModel>()
            .DistinctBy(card => card.Id)
            .ToList();
    }

    private static IEnumerable<Creature> GetLargesseTargets(CardModel source)
    {
        return source.CombatState?.PlayerCreatures
            .Where(creature => IsValidLargesseTarget(source, creature)) ?? [];
    }

    private static bool IsValidLargesseTarget(CardModel source, Creature? target)
    {
        return target is { IsHittable: true, IsPlayer: true } &&
            target != source.Owner.Creature &&
            source.IsValidTarget(target);
    }

    private static CardModel? PredictLargesseCard(CardModel source, Player target, Rng sourceRng)
    {
        var card = PredictionUtils.TakeRandomDistinctForCombat(
                target,
                ModelDb.CardPool<ColorlessCardPool>()
                    .GetUnlockedCards(target.UnlockState, target.RunState.CardMultiplayerConstraint),
                1,
                PredictionUtils.CloneRng(sourceRng))
            .FirstOrDefault();
        if (card == null)
        {
            return null;
        }

        var preview = PredictionUtils.CreatePreviewCard(card, target);
        if (source.IsUpgraded)
        {
            PredictionUtils.UpgradePreviewCardInPlace(preview);
        }

        return preview;
    }
}
