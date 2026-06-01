using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Random;

namespace RandomForeseer;

internal static class CombatCardPrediction
{
    public static IReadOnlyList<IHoverTip> GetHoverTips(CardModel card)
    {
        if (!RandomForeseerSettings.EnableCombatCardPrediction ||
            !card.IsMutable ||
            card.Pile?.Type != PileType.Hand ||
            card.Owner.Creature.CombatState == null)
        {
            return [];
        }

        var previewRng = PredictionUtils.CloneRng(card.Owner.RunState.Rng.CombatCardGeneration);
        var cards = PredictCards(card, previewRng);
        return cards.Select(predictedCard => (IHoverTip)new PredictionCardHoverTip(predictedCard)).ToList();
    }

    private static IReadOnlyList<CardModel> PredictCards(CardModel card, Rng previewRng)
    {
        return card switch
        {
            BundleOfJoy => PredictColorlessCards(card, card.DynamicVars.Cards.IntValue, previewRng),
            Discovery => PredictFreeCharacterCards(card, null, 3, previewRng),
            Distraction => PredictFreeCharacterCards(card, CardType.Skill, 1, previewRng),
            InfernalBlade => PredictFreeCharacterCards(card, CardType.Attack, 1, previewRng),
            JackOfAllTrades => PredictJackOfAllTrades(card, previewRng),
            Jackpot => PredictJackpot(card, previewRng),
            MadScience madScience when madScience.TinkerTimeRider == TinkerTime.RiderEffect.Chaos =>
                PredictCharacterCards(card, null, 1, previewRng).Select(PredictionUtils.ToFreeThisTurnPreviewCard).ToList(),
            ManifestAuthority => PredictManifestAuthority(card, previewRng),
            Metamorphosis => PredictMetamorphosis(card, previewRng),
            Quasar => PredictQuasar(card, previewRng),
            Splash => PredictSplash(card, previewRng),
            Stoke => PredictStoke(card, previewRng),
            WhiteNoise => PredictFreeCharacterCards(card, CardType.Power, 1, previewRng),
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

    private static IReadOnlyList<CardModel> PredictFreeCharacterCards(
        CardModel source,
        CardType? type,
        int count,
        Rng previewRng)
    {
        return PredictCharacterCards(source, type, count, previewRng)
            .Select(PredictionUtils.ToFreeThisTurnPreviewCard)
            .ToList();
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

        return PredictionUtils.TakeRandomForCombat(owner, candidates, source.DynamicVars.Cards.IntValue, previewRng)
            .Select(ToFreeThisCombatPreviewCard)
            .ToList();
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
            ? cards.Select(ToUpgradedAndFreeThisTurnPreviewCard).ToList()
            : cards.Select(PredictionUtils.ToFreeThisTurnPreviewCard).ToList();
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

    private static CardModel ToUpgradedAndFreeThisTurnPreviewCard(CardModel card)
    {
        var previewCard = PredictionUtils.ToUpgradedPreviewCard(card);
        previewCard.SetToFreeThisTurn();
        return previewCard;
    }

    private static CardModel ToFreeThisCombatPreviewCard(CardModel card)
    {
        var previewCard = card.ToMutable();
        previewCard.SetToFreeThisCombat();
        return previewCard;
    }
}

internal class PredictionCardHoverTip(CardModel card) : CardHoverTip(card), IHoverTip
{
    bool IHoverTip.IsInstanced => true;
}
