using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using RandomForeseer.Common;

namespace RandomForeseer.InCombat;

[HarmonyPatch(typeof(CardModel), nameof(CardModel.HoverTips), MethodType.Getter)]
internal static class CombatCardPredictionHoverTipsPatch
{
    private static readonly PredictionHoverTipRegistry<CardModel> CombatPlayPredictionProviders = new();

    private static readonly PredictionHoverTipRegistry<CardModel> CombatTransformPredictionProviders = new();

    static CombatCardPredictionHoverTipsPatch()
    {
        CombatPlayPredictionProviders.Register("combat card generation", CombatCardGenerationPrediction.GetCardHoverTips);
        CombatPlayPredictionProviders.Register("combat potion generation", PotionGenerationPrediction.GetCardHoverTips);
        CombatPlayPredictionProviders.Register("combat card selection", CombatCardSelectionPrediction.GetHoverTips);
        CombatPlayPredictionProviders.Register("auto-play from draw pile", AutoPlayFromDrawPilePrediction.GetCardHoverTips);
        CombatPlayPredictionProviders.Register("card draw", CardDrawPrediction.GetCardHoverTips);

        CombatTransformPredictionProviders.Register("combat transform selection", CombatTransformPrediction.GetCardHoverTips);
    }

    private static void Postfix(CardModel __instance, ref IEnumerable<IHoverTip> __result)
    {
        var predictionTips = GetPredictionHoverTips(__instance);
        if (predictionTips.Count > 0)
        {
            __result = __result.Concat(predictionTips);
        }
    }

    public static IReadOnlyList<IHoverTip> GetPredictionHoverTips(CardModel card)
    {
        if (!card.IsMutable)
        {
            return [];
        }

        var predictionTips = new List<IHoverTip>();

        if (ShouldShowCombatPlayPredictionHoverTips(card))
        {
            predictionTips.AddRange(CombatPlayPredictionProviders.GetHoverTips(card));
        }

        predictionTips.AddRange(CombatTransformPredictionProviders.GetHoverTips(card));

        return predictionTips;
    }

    private static bool ShouldShowCombatPlayPredictionHoverTips(CardModel card)
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
