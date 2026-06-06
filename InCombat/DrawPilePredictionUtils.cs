using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Random;
using RandomForeseer.Common;

namespace RandomForeseer.InCombat;

internal static class DrawPilePredictionUtils
{
    public static DrawPilePredictionResult PredictTopCardsAfterNecessaryShuffles(Player player, int count)
    {
        if (count <= 0 || player.Creature.CombatState is not { } combatState)
        {
            return DrawPilePredictionResult.Empty;
        }

        var previewShuffleRng = PredictionUtils.CloneRng(player.RunState.Rng.Shuffle);
        var drawPileCards = PileType.Draw.GetPile(player).Cards.ToList();
        var discardPileCards = PileType.Discard.GetPile(player).Cards.ToList();
        var predictedCards = new List<CardModel>();
        var hasDriftRisk = false;

        for (var i = 0; i < count; i++)
        {
            if (drawPileCards.Count == 0)
            {
                if (discardPileCards.Count == 0)
                {
                    break;
                }

                var shuffleResult = PredictShuffle(combatState, player, discardPileCards, previewShuffleRng);
                drawPileCards = shuffleResult.Cards.ToList();
                hasDriftRisk |= shuffleResult.HasDriftRisk;
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

        return new DrawPilePredictionResult(predictedCards, hasDriftRisk);
    }

    private static DrawPilePredictionResult PredictShuffle(
        ICombatState combatState,
        Player player,
        IEnumerable<CardModel> discardPileCards,
        Rng previewShuffleRng)
    {
        var shuffledCards = discardPileCards.ToList();
        shuffledCards.StableShuffle(previewShuffleRng);
        Hook.ModifyShuffleOrder(combatState, player, shuffledCards, isInitialShuffle: false);
        var hasDriftRisk = SimulateAfterShuffleListeners(combatState, player, shuffledCards, previewShuffleRng);
        return new DrawPilePredictionResult(shuffledCards, hasDriftRisk);
    }

    private static bool SimulateAfterShuffleListeners(
        ICombatState combatState,
        Player player,
        List<CardModel> drawPileCards,
        Rng previewShuffleRng)
    {
        var hasDriftRisk = false;
        foreach (var model in Hook.IterateCombatHookListeners(combatState))
        {
            switch (model)
            {
                case BiiigHug { Owner: { } owner } when owner == player:
                    var soot = PredictionUtils.CreateCard(ModelDb.Card<Soot>(), player);
                    drawPileCards.Insert(previewShuffleRng.NextInt(drawPileCards.Count + 1), soot);
                    break;
                case StratagemPower { Owner.Player: { } powerOwner } when powerOwner == player:
                case TheAbacus { Owner: { } relicOwner } when relicOwner == player:
                    hasDriftRisk = true;
                    break;
            }
        }

        return hasDriftRisk;
    }
}

internal sealed record DrawPilePredictionResult(IReadOnlyList<CardModel> Cards, bool HasDriftRisk)
{
    public static DrawPilePredictionResult Empty { get; } = new([], false);
}
