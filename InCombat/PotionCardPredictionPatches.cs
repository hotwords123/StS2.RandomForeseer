using HarmonyLib;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace RandomForeseer.InCombat;

[HarmonyPatch(typeof(PotionModel), nameof(PotionModel.HoverTips), MethodType.Getter)]
internal static class PotionCardPredictionHoverTipsPatch
{
    private static void Postfix(PotionModel __instance, ref IEnumerable<IHoverTip> __result)
    {
        IReadOnlyList<IHoverTip> predictionTips;
        try
        {
            predictionTips = PotionCardPrediction.GetHoverTips(__instance);
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"Potion card prediction failed for {__instance.Id}: {ex}");
            return;
        }

        if (predictionTips.Count > 0)
        {
            __result = __result.Concat(predictionTips);
        }
    }
}
