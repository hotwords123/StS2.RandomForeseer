using HarmonyLib;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using RandomForeseer.Common;

namespace RandomForeseer.InCombat;

[HarmonyPatch(typeof(CardModel), nameof(CardModel.HoverTips), MethodType.Getter)]
internal static class CombatCardPredictionHoverTipsPatch
{
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
            AddCombatPlayPredictionHoverTips(card, predictionTips);
        }

        AddCombatTransformPredictionHoverTips(card, predictionTips);

        return predictionTips;
    }

    private static void AddCombatPlayPredictionHoverTips(CardModel card, List<IHoverTip> predictionTips)
    {
        try
        {
            predictionTips.AddRange(CombatCardGenerationPrediction.GetCardHoverTips(card));
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"Combat card generation prediction failed for {card.Id}: {ex}");
        }

        try
        {
            predictionTips.AddRange(PotionGenerationPrediction.GetCardHoverTips(card));
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"Combat potion generation prediction failed for {card.Id}: {ex}");
        }

        try
        {
            predictionTips.AddRange(CombatCardSelectionPrediction.GetHoverTips(card));
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"Combat card selection prediction failed for {card.Id}: {ex}");
        }

        try
        {
            predictionTips.AddRange(AutoPlayFromDrawPilePrediction.GetCardHoverTips(card));
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"Auto-play from draw pile prediction failed for {card.Id}: {ex}");
        }
    }

    private static void AddCombatTransformPredictionHoverTips(CardModel card, List<IHoverTip> predictionTips)
    {
        try
        {
            predictionTips.AddRange(CombatTransformPrediction.GetCardHoverTips(card));
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"Combat transform selection prediction failed for {card.Id}: {ex}");
        }
    }

    private static bool ShouldShowCombatPlayPredictionHoverTips(CardModel card)
    {
        var hand = NPlayerHand.Instance;
        return hand?.CurrentMode == NPlayerHand.Mode.Play && hand.GetCardHolder(card) is NHandCardHolder;
    }
}

[HarmonyPatch(typeof(NMouseCardPlay), "StartCardDrag")]
internal static class CombatCardPredictionMouseDragHoverTipsPatch
{
    private static void Postfix(NMouseCardPlay __instance)
    {
        if (__instance.Holder is not { } holder ||
            holder.CardModel is not { } card)
        {
            return;
        }

        var predictionTips = CombatCardPredictionHoverTipsPatch.GetPredictionHoverTips(card);
        if (predictionTips.Count <= 0)
        {
            return;
        }

        NHoverTipSet.CreateAndShow(holder, predictionTips)?.SetAlignmentForCardHolder(holder);
    }
}
