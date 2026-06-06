using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using RandomForeseer.Common;
using RandomForeseer.Common.Hooks;
using RandomForeseer.InCombat.Hooks;

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

        var hookResults = AfterShuffleHook.Run(new AfterShuffleHookContext
        {
            CombatState = combatState,
            Player = player,
            DrawPileCards = shuffledCards,
            ShuffleRng = previewShuffleRng
        });

        var hasDriftRisk = hookResults.Any(
            result => result.Kind is HookResultKind.DriftRisk or HookResultKind.Unsupported);
        return new DrawPilePredictionResult(shuffledCards, hasDriftRisk);
    }
}

internal sealed record DrawPilePredictionResult(IReadOnlyList<CardModel> Cards, bool HasDriftRisk)
{
    public static DrawPilePredictionResult Empty { get; } = new([], false);
}
