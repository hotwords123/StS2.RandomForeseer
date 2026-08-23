using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Potions;
using RandomForeseer.RandomForeseerCode.Telemetry;

namespace RandomForeseer.RandomForeseerCode.InCombat.Patches;

[HarmonyPatch(typeof(NPotionHolder))]
internal static class CombatPotionPredictionHolderPatches
{
    // This prefix must establish the session before vanilla OnFocus reads PotionModel.HoverTips.
    [HarmonyPatch("OnFocus")]
    [HarmonyPrefix]
    private static void BeginHoverPrediction(NPotionHolder __instance)
    {
        try
        {
            CombatPotionPredictionController.OnPotionFocus(__instance);
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"Combat potion prediction failed on focus: {ex}");
            ModTelemetry.CaptureException(
                ex,
                "combat_potion_prediction",
                "begin_hover_prediction",
                CombatPotionPredictionTelemetry.GetTelemetryContext(__instance));
        }
    }

    [HarmonyPatch("OnUnfocus")]
    [HarmonyPrefix]
    private static void EndHoverPrediction(NPotionHolder __instance)
    {
        try
        {
            CombatPotionPredictionController.OnPotionUnfocus(__instance);
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"Combat potion prediction failed on unfocus: {ex}");
            ModTelemetry.CaptureException(
                ex,
                "combat_potion_prediction",
                "end_hover_prediction",
                CombatPotionPredictionTelemetry.GetTelemetryContext(__instance));
        }
    }

    // A prefix is required before TargetNode calls StartTargeting: StartTargeting re-emits an already-focused
    // creature, and TargetNode may synchronously focus a creature or multiplayer nameplate immediately afterward.
    [HarmonyPatch(nameof(NPotionHolder.TargetNode))]
    [HarmonyPrefix]
    private static void BeginTargetPrediction(NPotionHolder __instance)
    {
        try
        {
            CombatPotionPredictionController.OnPotionTargetingStart(__instance);
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"Combat potion prediction failed on targeting start: {ex}");
            ModTelemetry.CaptureException(
                ex,
                "combat_potion_prediction",
                "begin_target_prediction",
                CombatPotionPredictionTelemetry.GetTelemetryContext(__instance));
        }
    }

    // Clear prediction state before vanilla replaces the potion with the empty-slot presentation.
    [HarmonyPatch(nameof(NPotionHolder.RemoveUsedPotion))]
    [HarmonyPrefix]
    private static void EndUsedPotionPrediction(NPotionHolder __instance)
    {
        try
        {
            CombatPotionPredictionController.OnPotionRemoved(__instance);
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"Combat potion prediction failed on used potion removal: {ex}");
            ModTelemetry.CaptureException(
                ex,
                "combat_potion_prediction",
                "end_used_potion_prediction",
                CombatPotionPredictionTelemetry.GetTelemetryContext(__instance));
        }
    }

    [HarmonyPatch(nameof(NPotionHolder.DiscardPotion))]
    [HarmonyPrefix]
    private static void EndDiscardedPotionPrediction(NPotionHolder __instance)
    {
        try
        {
            CombatPotionPredictionController.OnPotionRemoved(__instance);
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"Combat potion prediction failed on discarded potion removal: {ex}");
            ModTelemetry.CaptureException(
                ex,
                "combat_potion_prediction",
                "end_discarded_potion_prediction",
                CombatPotionPredictionTelemetry.GetTelemetryContext(__instance));
        }
    }
}

[HarmonyPatch(typeof(NPotionPopup))]
internal static class CombatPotionPredictionPopupPatches
{
    // NPotionPopup._Ready shows hover tips for the potion, so a prefix is required to establish the session
    // before vanilla reads PotionModel.HoverTips.
    [HarmonyPatch(nameof(NPotionPopup.Create))]
    [HarmonyPrefix]
    private static void BeginActionPrediction(NPotionHolder holder)
    {
        try
        {
            CombatPotionPredictionController.OnPotionPopupOpen(holder);
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"Combat potion prediction failed on popup open: {ex}");
            ModTelemetry.CaptureException(
                ex,
                "combat_potion_prediction",
                "begin_action_prediction",
                CombatPotionPredictionTelemetry.GetTelemetryContext(holder));
        }
    }

    [HarmonyPatch(nameof(NPotionPopup.Remove))]
    [HarmonyPrefix]
    private static void EndActionPrediction(NPotionPopup __instance)
    {
        try
        {
            CombatPotionPredictionController.OnPotionPopupClose(__instance._holder);
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"Combat potion prediction failed on popup close: {ex}");
            ModTelemetry.CaptureException(
                ex,
                "combat_potion_prediction",
                "end_action_prediction",
                CombatPotionPredictionTelemetry.GetTelemetryContext(__instance));
        }
    }

    [HarmonyPatch(nameof(NPotionPopup._ExitTree))]
    [HarmonyPrefix]
    private static void EndExitedPopupPrediction(NPotionPopup __instance)
    {
        try
        {
            CombatPotionPredictionController.OnPotionPopupClose(__instance._holder);
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"Combat potion prediction failed on popup exit: {ex}");
            ModTelemetry.CaptureException(
                ex,
                "combat_potion_prediction",
                "end_exited_popup_prediction",
                CombatPotionPredictionTelemetry.GetTelemetryContext(__instance));
        }
    }
}

internal static class CombatPotionPredictionTelemetry
{
    public static object? GetTelemetryContext(NPotionHolder holder)
    {
        return holder is { Potion.Model: { } potion }
            ? TelemetryContext.ForModel(potion)
            : null;
    }

    public static object? GetTelemetryContext(NPotionPopup popup)
    {
        return GetTelemetryContext(popup._holder);
    }
}
