using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.HoverTips;

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
        if (!ShouldShowPredictionHoverTips(card))
        {
            return [];
        }

        var predictionTips = new List<IHoverTip>();

        try
        {
            predictionTips.AddRange(CombatCardGenerationPrediction.GetHoverTips(card));
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

        return predictionTips;
    }

    private static bool ShouldShowPredictionHoverTips(CardModel card)
    {
        if (!card.IsMutable)
        {
            return false;
        }

        var hand = NPlayerHand.Instance;
        return hand?.CurrentMode == NPlayerHand.Mode.Play &&
            card.Owner?.PlayerCombatState?.Phase == PlayerTurnPhase.Play &&
            hand.GetCardHolder(card) is NHandCardHolder;
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
