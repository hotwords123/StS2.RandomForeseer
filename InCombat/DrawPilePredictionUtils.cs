using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using RandomForeseer.Common;

namespace RandomForeseer.InCombat;

internal static class DrawPilePredictionUtils
{
    public static IReadOnlyList<CardModel> PredictTopCardsAfterNecessaryShuffles(Player player, int count)
    {
        if (count <= 0 || player.Creature.CombatState is not { } combatState)
        {
            return [];
        }

        var previewShuffleRng = PredictionUtils.CloneRng(player.RunState.Rng.Shuffle);
        var drawPileCards = PileType.Draw.GetPile(player).Cards.ToList();
        var discardPileCards = PileType.Discard.GetPile(player).Cards.ToList();
        var predictedCards = new List<CardModel>();

        for (var i = 0; i < count; i++)
        {
            if (drawPileCards.Count == 0)
            {
                if (discardPileCards.Count == 0)
                {
                    break;
                }

                drawPileCards = PredictShuffle(combatState, player, discardPileCards, previewShuffleRng);
                discardPileCards.Clear();
            }

            if (drawPileCards.Count == 0)
            {
                break;
            }

            var card = drawPileCards[0];
            drawPileCards.RemoveAt(0);
            predictedCards.Add(card);
        }

        return predictedCards;
    }

    private static List<CardModel> PredictShuffle(
        ICombatState combatState,
        Player player,
        IEnumerable<CardModel> discardPileCards,
        Rng previewShuffleRng)
    {
        var shuffledCards = discardPileCards.ToList();
        shuffledCards.StableShuffle(previewShuffleRng);
        Hook.ModifyShuffleOrder(combatState, player, shuffledCards, isInitialShuffle: false);
        return shuffledCards;
    }
}
