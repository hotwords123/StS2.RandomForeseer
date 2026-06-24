using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using RandomForeseer.Common;

namespace RandomForeseer.InCombat;

internal static class CombatCardPrediction
{
    private static readonly PredictionHoverTipRegistry<CardModel> CombatPlayPredictionProviders = new();

    private static readonly PredictionHoverTipRegistry<CardModel> CombatTransformPredictionProviders = new();

    static CombatCardPrediction()
    {
        CombatPlayPredictionProviders.Register("combat card generation", CombatCardGenerationPrediction.GetCardHoverTips);
        CombatPlayPredictionProviders.Register("combat potion generation", PotionGenerationPrediction.GetCardHoverTips);
        CombatPlayPredictionProviders.Register("combat card selection", CombatCardSelectionPrediction.GetHoverTips);
        CombatPlayPredictionProviders.Register("auto-play from draw pile", AutoPlayFromDrawPilePrediction.GetCardHoverTips);
        CombatPlayPredictionProviders.Register("card draw", CardDrawPrediction.GetCardHoverTips);

        CombatTransformPredictionProviders.Register("combat transform selection", CombatTransformPrediction.GetCardHoverTips);
    }

    public static IReadOnlyList<IHoverTip> GetHoverTips(CardModel card)
    {
        if (!card.IsMutable)
        {
            return [];
        }

        var predictionTips = new List<IHoverTip>();

        if (ShouldShowCombatPlayPrediction(card))
        {
            predictionTips.AddRange(CombatPlayPredictionProviders.GetHoverTips(card));
        }

        predictionTips.AddRange(CombatTransformPredictionProviders.GetHoverTips(card));

        return predictionTips;
    }

    private static bool ShouldShowCombatPlayPrediction(CardModel card)
    {
        if (card.Owner?.Creature.CombatState == null)
        {
            return false;
        }

        if (ChooseACardPredictionContext.Contains(card))
        {
            // Cards shown by NChooseACardSelectionScreen are generated mutable cards with an owner,
            // but they are not in any combat pile yet. Treat them like cards that will enter hand
            // after the player chooses them, so they can show the same play predictions as hand cards.
            return true;
        }

        if (card.Pile is { Type: PileType.Hand })
        {
            if (NPlayerHand.Instance is { } hand && hand.GetCardHolder(card) is { } localHolder)
            {
                // For the local hand UI, only show play predictions in normal play mode, not selection modes.
                return hand.CurrentMode == NPlayerHand.Mode.Play && localHolder is NHandCardHolder;
            }

            // If no local holder exists, fall back to allowing prediction. This preserves existing
            // behavior for non-local or integration-provided hand card views.
            return true;
        }

        return false;
    }
}
