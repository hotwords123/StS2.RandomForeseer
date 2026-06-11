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
        if (card.Pile?.Type != PileType.Hand || card.Owner.Creature.CombatState == null)
        {
            return false;
        }

        if (NPlayerHand.Instance is not { } hand || hand.GetCardHolder(card) is not { } localHolder)
        {
            return true;
        }

        return hand.CurrentMode == NPlayerHand.Mode.Play && localHolder is NHandCardHolder;
    }
}
