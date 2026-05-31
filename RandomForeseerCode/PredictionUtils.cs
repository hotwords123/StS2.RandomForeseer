using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.Models;
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
