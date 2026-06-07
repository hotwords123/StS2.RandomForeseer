using HarmonyLib;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using RandomForeseer.Common;

namespace RandomForeseer.InCombat;

[HarmonyPatch(typeof(PotionModel), nameof(PotionModel.HoverTips), MethodType.Getter)]
internal static class PotionPredictionHoverTipsPatch
{
    private static void Postfix(PotionModel __instance, ref IEnumerable<IHoverTip> __result)
    {
        if (!__instance.IsMutable || __instance.Owner == null || __instance.Owner.RunState is not RunState)
        {
            return;
        }

        IReadOnlyList<IHoverTip> generatedCardPredictionTips;
        try
        {
            generatedCardPredictionTips = CombatCardGenerationPrediction.GetPotionHoverTips(__instance);
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

        IReadOnlyList<IHoverTip> autoPlayPredictionTips;
        try
        {
            autoPlayPredictionTips = AutoPlayFromDrawPilePrediction.GetPotionHoverTips(__instance);
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"Potion auto-play from draw pile prediction failed for {__instance.Id}: {ex}");
            return;
        }

        if (autoPlayPredictionTips.Count > 0)
        {
            __result = __result.Concat(autoPlayPredictionTips);
        }

        IReadOnlyList<IHoverTip> drawPilePredictionTips;
        try
        {
            drawPilePredictionTips = PotionDrawPrediction.GetPotionHoverTips(__instance);
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"Potion draw-pile prediction failed for {__instance.Id}: {ex}");
            return;
        }

        if (drawPilePredictionTips.Count > 0)
        {
            __result = __result.Concat(drawPilePredictionTips);
        }
    }
}
