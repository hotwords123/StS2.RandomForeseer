using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using RandomForeseer.RandomForeseerCode.Common;

namespace RandomForeseer.RandomForeseerCode.InCombat;

internal static class CombatCardPrediction
{
    private static readonly PredictionHoverTipRegistry<CardModel> CombatPlayPredictionProviders = CreatePlayRegistry();

    private static PredictionHoverTipRegistry<CardModel> CreatePlayRegistry()
    {
        var registry = new PredictionHoverTipRegistry<CardModel>();

        registry.Register("combat card generation", CombatCardGenerationPrediction.GetCardHoverTips);
        registry.Register("combat potion generation", PotionGenerationPrediction.GetCardHoverTips);
        registry.Register("combat card selection", CombatCardSelectionPrediction.GetHoverTips);
        registry.Register("auto-play from draw pile", AutoPlayFromDrawPilePrediction.GetCardHoverTips);
        registry.Register("card draw", CardDrawPrediction.GetCardHoverTips);
        registry.Register("orb", OrbPrediction.GetHoverTips);
        registry.Register("random target attack", RandomTargetAttackPrediction.GetHoverTips);

        return registry;
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

        try
        {
            predictionTips.AddRange(CombatTransformPrediction.GetCardHoverTips(card));
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"Combat transform prediction failed for {card.Id}: {ex}");
        }

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
