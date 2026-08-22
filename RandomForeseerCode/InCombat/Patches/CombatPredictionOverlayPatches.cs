using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace RandomForeseer.RandomForeseerCode.InCombat.Patches;

[HarmonyPatch(typeof(NCombatRoom), nameof(NCombatRoom.RemoveCreatureNode))]
internal static class CombatPredictionOverlayRefreshOnCreatureRemovedPatch
{
    private static void Postfix()
    {
        CombatPredictionOverlay.RefreshPositions();
    }
}
