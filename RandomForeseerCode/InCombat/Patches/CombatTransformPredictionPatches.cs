using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using RandomForeseer.RandomForeseerCode.Common.HoverTips;
using RandomForeseer.RandomForeseerCode.Telemetry;

namespace RandomForeseer.RandomForeseerCode.InCombat.Patches;

[HarmonyPatch(typeof(NPlayerHand))]
internal static class CombatTransformPredictionPlayerHandPatch
{
    [HarmonyPatch(nameof(NPlayerHand.SelectCards))]
    [HarmonyPrefix]
    private static void BeginSession(NPlayerHand __instance, CardSelectorPrefs prefs, AbstractModel? source)
    {
        try
        {
            CombatTransformPrediction.BeginSession(__instance, prefs, source);
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"Combat transform prediction failed to begin session: {ex}");
            ModTelemetry.CaptureException(
                ex,
                "combat_transform_prediction",
                "begin_session",
                source is not null ? TelemetryContext.ForModel(source) : null);
        }
    }

    [HarmonyPatch(nameof(NPlayerHand.AfterCardsSelected))]
    [HarmonyPrefix]
    private static void EndSession(NPlayerHand __instance)
    {
        try
        {
            CombatTransformPrediction.EndSession(__instance);
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"Combat transform prediction failed to end session: {ex}");
            ModTelemetry.CaptureException(ex, "combat_transform_prediction", "end_session");
        }
    }

    [HarmonyPatch(nameof(NPlayerHand._ExitTree))]
    [HarmonyPrefix]
    private static void CleanupSession(NPlayerHand __instance)
    {
        try
        {
            CombatTransformPrediction.EndSession(__instance);
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"Combat transform prediction failed to cleanup session: {ex}");
            ModTelemetry.CaptureException(ex, "combat_transform_prediction", "cleanup_session");
        }
    }
}

[HarmonyPatch(typeof(NSelectedHandCardHolder), "CreateHoverTips")]
internal static class CombatTransformPredictionSelectedHoverTipsPatch
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(NSelectedHandCardHolder __instance)
    {
        PredictionHoverTipSetHelper.EnsureHoverTipSet(__instance)?.SetAlignmentForCardHolder(__instance);
    }
}
