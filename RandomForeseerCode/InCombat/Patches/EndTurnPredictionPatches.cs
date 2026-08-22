using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace RandomForeseer.RandomForeseerCode.InCombat.Patches;

[HarmonyPatch(typeof(NCombatRoom))]
internal static class EndTurnPredictionCombatRoomPatches
{
    [HarmonyPatch("_EnterTree")]
    [HarmonyPostfix]
    private static void Subscribe()
    {
        EndTurnPredictionController.Subscribe();
    }

    [HarmonyPatch("_ExitTree")]
    [HarmonyPostfix]
    private static void Unsubscribe()
    {
        EndTurnPredictionController.Unsubscribe();
    }
}

[HarmonyPatch(typeof(NEndTurnButton))]
internal static class EndTurnPredictionButtonPatches
{
    [HarmonyPatch("OnFocus")]
    [HarmonyPostfix]
    private static void OnFocus(NEndTurnButton __instance)
    {
        EndTurnPredictionController.OnEndTurnButtonFocused(__instance);
    }

    [HarmonyPatch("OnUnfocus")]
    [HarmonyPostfix]
    private static void OnUnfocus()
    {
        EndTurnPredictionController.OnEndTurnButtonUnfocused();
    }
}

[HarmonyPatch(typeof(Creature), nameof(Creature.HoverTips), MethodType.Getter)]
internal static class EndTurnPredictionCreatureHoverTipsPatch
{
    private static void Postfix(Creature __instance, ref IEnumerable<IHoverTip> __result)
    {
        var predictionTips = EndTurnPredictionCreatureHoverTips.GetHoverTips(__instance);
        if (predictionTips.Count > 0)
        {
            __result = __result.Concat(predictionTips);
        }
    }
}
