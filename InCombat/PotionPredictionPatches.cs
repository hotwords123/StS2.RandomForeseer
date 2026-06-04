using HarmonyLib;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace RandomForeseer.InCombat;

[HarmonyPatch(typeof(PotionModel), nameof(PotionModel.HoverTips), MethodType.Getter)]
internal static class PotionPredictionHoverTipsPatch
{
    private static void Postfix(PotionModel __instance, ref IEnumerable<IHoverTip> __result)
    {
        IReadOnlyList<IHoverTip> generatedCardPredictionTips;
        try
        {
            generatedCardPredictionTips = PotionCardPrediction.GetHoverTips(__instance);
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"Potion card prediction failed for {__instance.Id}: {ex}");
            return;
        }

        if (generatedCardPredictionTips.Count > 0)
        {
            __result = __result.Concat(generatedCardPredictionTips);
        }

        IReadOnlyList<IHoverTip> generatedPotionPredictionTips;
        try
        {
            generatedPotionPredictionTips = PotionGenerationPrediction.GetPotionHoverTips(__instance);
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"Potion generation prediction failed for {__instance.Id}: {ex}");
            return;
        }

        if (generatedPotionPredictionTips.Count > 0)
        {
            __result = __result.Concat(generatedPotionPredictionTips);
        }
    }
}
