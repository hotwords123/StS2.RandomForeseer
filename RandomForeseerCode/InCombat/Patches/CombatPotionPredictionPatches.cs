using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Potions;

namespace RandomForeseer.RandomForeseerCode.InCombat.Patches;

[HarmonyPatch(typeof(NPotionHolder))]
internal static class CombatPotionPredictionHolderPatches
{
    // This prefix must establish the session before vanilla OnFocus reads PotionModel.HoverTips.
    [HarmonyPatch("OnFocus")]
    [HarmonyPrefix]
    private static void BeginHoverPrediction(NPotionHolder __instance)
    {
        if (!__instance._isFocused)
        {
            CombatPotionPredictionController.OnPotionFocus(__instance);
        }
    }

    [HarmonyPatch("OnUnfocus")]
    [HarmonyPrefix]
    private static void EndHoverPrediction(NPotionHolder __instance)
    {
        CombatPotionPredictionController.OnPotionUnfocus(__instance);
    }

    // A prefix is required before TargetNode calls StartTargeting: StartTargeting re-emits an already-focused
    // creature, and TargetNode may synchronously focus a creature or multiplayer nameplate immediately afterward.
    [HarmonyPatch(nameof(NPotionHolder.TargetNode))]
    [HarmonyPrefix]
    private static void BeginTargetPrediction(NPotionHolder __instance)
    {
        CombatPotionPredictionController.OnPotionTargetingStart(__instance);
    }

    // Clear prediction state before vanilla replaces the potion with the empty-slot presentation.
    [HarmonyPatch(nameof(NPotionHolder.RemoveUsedPotion))]
    [HarmonyPrefix]
    private static void EndUsedPotionPrediction(NPotionHolder __instance)
    {
        CombatPotionPredictionController.OnPotionRemoved(__instance);
    }

    [HarmonyPatch(nameof(NPotionHolder.DiscardPotion))]
    [HarmonyPrefix]
    private static void EndDiscardedPotionPrediction(NPotionHolder __instance)
    {
        CombatPotionPredictionController.OnPotionRemoved(__instance);
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
        CombatPotionPredictionController.OnPotionPopupOpen(holder);
    }

    [HarmonyPatch(nameof(NPotionPopup.Remove))]
    [HarmonyPrefix]
    private static void EndActionPrediction(NPotionPopup __instance)
    {
        CombatPotionPredictionController.OnPotionPopupClose(__instance._holder);
    }

    [HarmonyPatch(nameof(NPotionPopup._ExitTree))]
    [HarmonyPrefix]
    private static void EndExitedPopupPrediction(NPotionPopup __instance)
    {
        CombatPotionPredictionController.OnPotionPopupClose(__instance._holder);
    }
}
