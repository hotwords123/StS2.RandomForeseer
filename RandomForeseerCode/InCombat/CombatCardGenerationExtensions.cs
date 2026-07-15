using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using RandomForeseer.RandomForeseerCode.Common;

namespace RandomForeseer.RandomForeseerCode.InCombat;

internal static class CombatCardGenerationExtensions
{
    public static IEnumerable<CardModel> FilterForCombatAndPlayerCount(
        this IEnumerable<CardModel> cards,
        Player player)
    {
        return CardFactory.FilterForPlayerCount(player.RunState, CardFactory.FilterForCombat(cards));
    }

    // Mirrors CardFactory.GetDistinctForCombat, but does not create cards for the player.
    public static IEnumerable<CardModel> TakeRandomDistinctForCombat(
        this IEnumerable<CardModel> cards,
        Player player,
        int count,
        Rng rng)
    {
        return cards.FilterForCombatAndPlayerCount(player).TakeRandom(count, rng);
    }

    // Mirrors CardFactory.GetForCombat, but does not create cards for the player.
    public static IEnumerable<CardModel> TakeRandomForCombat(
        this IEnumerable<CardModel> cards,
        Player player,
        int count,
        Rng rng)
    {
        var options = cards.FilterForCombatAndPlayerCount(player).ToList();
        if (options.Count == 0)
        {
            return [];
        }

        List<CardModel> results = [];
        for (var i = 0; i < count; i++)
        {
            results.Add(rng.NextItem(options)!);
        }

        return results;
    }

    // Mirrors CardFactory.GetDistinctForCombat, but returns PredictedCard instead of CardModel.
    public static IEnumerable<PredictedCard> GetDistinctForCombat(
        this IEnumerable<CardModel> cards,
        Player player,
        int count,
        Rng rng)
    {
        return cards
            .TakeRandomDistinctForCombat(player, count, rng)
            .Select(card => PredictedCard.Create(card, player));
    }

    // Mirrors CardFactory.GetForCombat, but returns PredictedCard instead of CardModel.
    public static IEnumerable<PredictedCard> GetForCombat(
        this IEnumerable<CardModel> cards,
        Player player,
        int count,
        Rng rng)
    {
        return cards
            .TakeRandomForCombat(player, count, rng)
            .Select(card => PredictedCard.Create(card, player));
    }
}
