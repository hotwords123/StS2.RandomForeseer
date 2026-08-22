using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using RandomForeseer.RandomForeseerCode.Telemetry;

namespace RandomForeseer.RandomForeseerCode.InCombat.Patches;

[HarmonyPatch(typeof(CardModel), nameof(CardModel.HoverTips), MethodType.Getter)]
internal static class CombatCardPredictionHoverTipsPatch
{
    private static void Postfix(CardModel __instance, ref IEnumerable<IHoverTip> __result)
    {
        var predictionTips = CombatCardPrediction.GetHoverTips(__instance);
        if (predictionTips.Count > 0)
        {
            __result = __result.Concat(predictionTips);
        }
    }
}

[HarmonyPatch(typeof(NHandCardHolder))]
internal static class CombatCardPredictionHandPatches
{
    [HarmonyPatch("DoCardHoverEffects")]
    [HarmonyPrefix]
    private static void UpdatePredictionOnCardHover(NHandCardHolder __instance, bool isHovered)
    {
        try
        {
            CombatCardPredictionController.OnCardHover(__instance, isHovered);
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"Error updating combat card prediction on hover: {ex}");
            ModTelemetry.CaptureException(
                ex,
                "combat_card_prediction_controller",
                "on_card_hover",
                __instance is { CardModel: { } card } ? TelemetryContext.ForModel(card) : null);
        }
    }
}

[HarmonyPatch(typeof(NPlayerHand))]
internal static class CombatCardPredictionPlayerHandPatches
{
    [HarmonyPatch(nameof(NPlayerHand.StartCardPlay))]
    [HarmonyPrefix]
    private static void UpdatePredictionsOnCardPlayStarted(NHandCardHolder holder)
    {
        try
        {
            CombatCardPredictionController.OnCardPlayStarted(holder);
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"Error updating combat card prediction on play start: {ex}");
            ModTelemetry.CaptureException(
                ex,
                "combat_card_prediction_controller",
                "on_card_play_started",
                holder is { CardModel: { } card } ? TelemetryContext.ForModel(card) : null);
        }
    }
}

[HarmonyPatch(typeof(NCardPlay))]
internal static class CombatCardPredictionCardPlayPatches
{
    [HarmonyPatch("Cleanup")]
    [HarmonyPostfix]
    private static void CleanupPredictions(NCardPlay __instance)
    {
        try
        {
            CombatCardPredictionController.OnCardPlayCleanedUp(__instance.Holder);
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"Error updating combat card prediction on play cleanup: {ex}");
            ModTelemetry.CaptureException(ex, "combat_card_prediction_controller", "on_card_play_cleaned_up");
        }
    }
}

[HarmonyPatch(typeof(NTargetManager))]
internal static class CombatCardPredictionTargetManagerPatches
{
    // NTargetManager also has a Vector2 overload for target pickers that only know a
    // screen position. Card play uses the Control overload with the card node, which
    // lets us verify that this targeting session belongs to the active dragged card.
    // This must run as a prefix: StartTargeting synchronously calls OnTargetingStarted
    // on every NCreature, and an already focused creature emits CreatureHovered there.
    // Subscribing in a postfix would miss that initial target event.
    [HarmonyPatch(
        nameof(NTargetManager.StartTargeting),
        typeof(TargetType),
        typeof(Control),
        typeof(TargetMode),
        typeof(Func<bool>),
        typeof(Func<Node, bool>))]
    [HarmonyPrefix]
    private static void ObservePredictionTargetsBeforeTargetingStarts(Control control)
    {
        try
        {
            CombatCardPredictionController.OnCardPlayTargetingStarting(control);
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"Error updating combat card prediction on targeting start: {ex}");
            ModTelemetry.CaptureException(
                ex,
                "combat_card_prediction_controller",
                "on_card_play_targeting_starting",
                control is NCard { Model: { } card } ? TelemetryContext.ForModel(card) : null);
        }
    }
}
