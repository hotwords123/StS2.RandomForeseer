using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Random;

namespace RandomForeseer;

internal static class PredictionUtils
{
    public static Rng CloneRng(Rng rng)
    {
        return new Rng(rng.Seed, rng.Counter);
    }

    public static IReadOnlyList<CardModel> TakeRandomDistinctForCombat(
        Player player,
        IEnumerable<CardModel> cards,
        int count,
        Rng rng)
    {
        return FilterForCombat(FilterForPlayerCount(player, cards))
            .ToList()
            .UnstableShuffle(rng)
            .Take(count)
            .ToList();
    }

    public static IReadOnlyList<CardModel> TakeRandomDistinctCharacterCardsForCombat(
        Player player,
        CardType? type,
        int count,
        Rng rng)
    {
        var candidates = GetUnlockedCharacterCards(player);
        if (type.HasValue)
        {
            candidates = candidates.Where(card => card.Type == type.Value);
        }

        return TakeRandomDistinctForCombat(player, candidates, count, rng);
    }

    public static IReadOnlyList<CardModel> TakeRandomDistinctColorlessCardsForCombat(
        Player player,
        int count,
        Rng rng)
    {
        return TakeRandomDistinctForCombat(player, GetUnlockedColorlessCards(player), count, rng);
    }

    public static IReadOnlyList<CardModel> TakeRandomForCombat(
        Player player,
        IEnumerable<CardModel> cards,
        int count,
        Rng rng)
    {
        var options = FilterForPlayerCount(player, FilterForCombat(cards)).ToList();
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

    public static IEnumerable<CardModel> GetUnlockedCharacterCards(Player player)
    {
        return player.Character.CardPool
            .GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint);
    }

    public static IEnumerable<CardModel> GetUnlockedColorlessCards(Player player)
    {
        return ModelDb.CardPool<ColorlessCardPool>()
            .GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint);
    }

    public static CardModel ToUpgradedPreviewCard(CardModel card)
    {
        var previewCard = (CardModel)card.MutableClone();
        UpgradePreviewCardInPlace(previewCard);
        return previewCard;
    }

    public static void UpgradePreviewCardInPlace(CardModel previewCard)
    {
        if (previewCard.IsUpgradable)
        {
            previewCard.UpgradeInternal();
            previewCard.FinalizeUpgradeInternal();
        }
    }

    public static CardModel ToFreeThisTurnPreviewCard(CardModel card)
    {
        var previewCard = (CardModel)card.MutableClone();
        previewCard.SetToFreeThisTurn();
        return previewCard;
    }

    private static IEnumerable<CardModel> FilterForPlayerCount(Player player, IEnumerable<CardModel> cards)
    {
        return player.RunState.Players.Count > 1
            ? cards.Where(card => card.MultiplayerConstraint != CardMultiplayerConstraint.SingleplayerOnly)
            : cards.Where(card => card.MultiplayerConstraint != CardMultiplayerConstraint.MultiplayerOnly);
    }

    private static IEnumerable<CardModel> FilterForCombat(IEnumerable<CardModel> cards)
    {
        return cards
            .Where(card => card.CanBeGeneratedInCombat)
            .Where(card => card.Rarity is not CardRarity.Basic and not CardRarity.Ancient and not CardRarity.Event)
            .Distinct();
    }
}
