using HarmonyLib;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace RandomForeseer;

[HarmonyPatch(typeof(CardModel), nameof(CardModel.HoverTips), MethodType.Getter)]
internal static class CombatCardPredictionHoverTipsPatch
{
    private static void Postfix(CardModel __instance, ref IEnumerable<IHoverTip> __result)
    {
        IReadOnlyList<IHoverTip> predictionTips;
        try
        {
            predictionTips = CombatCardPrediction.GetHoverTips(__instance);
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"Combat card prediction failed for {__instance.Id}: {ex}");
            return;
        }

        if (predictionTips.Count > 0)
        {
            __result = __result.Concat(predictionTips);
        }
    }
}
