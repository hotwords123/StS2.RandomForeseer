using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using RandomForeseer.RandomForeseerCode.Telemetry;

namespace RandomForeseer.RandomForeseerCode.InCombat.Patches;

[HarmonyPatch(typeof(NCombatRoom), nameof(NCombatRoom.RemoveCreatureNode))]
internal static class CombatPredictionOverlayRefreshOnCreatureRemovedPatch
{
    private static void Postfix()
    {
        try
        {
            CombatPredictionOverlay.RefreshPositions();
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"Combat prediction overlay refresh failed on creature removed: {ex}");
            ModTelemetry.CaptureException(
                ex,
                "combat_prediction_overlay",
                "refresh_on_creature_removed");
        }
    }
}
