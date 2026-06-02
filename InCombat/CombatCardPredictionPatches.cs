using HarmonyLib;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace RandomForeseer.InCombat;

[HarmonyPatch(typeof(CardModel), nameof(CardModel.HoverTips), MethodType.Getter)]
internal static class CombatCardPredictionHoverTipsPatch
{
    private static void Postfix(CardModel __instance, ref IEnumerable<IHoverTip> __result)
    {
        IReadOnlyList<IHoverTip> generatedCardPredictionTips;
        try
        {
            generatedCardPredictionTips = CombatCardGenerationPrediction.GetHoverTips(__instance);
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"Combat card generation prediction failed for {__instance.Id}: {ex}");
            return;
        }

        if (generatedCardPredictionTips.Count > 0)
        {
            __result = __result.Concat(generatedCardPredictionTips);
        }

        IReadOnlyList<IHoverTip> selectionPredictionTips;
        try
        {
            selectionPredictionTips = CombatCardSelectionPrediction.GetHoverTips(__instance);
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"Combat card selection prediction failed for {__instance.Id}: {ex}");
            return;
        }

        if (selectionPredictionTips.Count > 0)
        {
            __result = __result.Concat(selectionPredictionTips);
        }
    }
}
