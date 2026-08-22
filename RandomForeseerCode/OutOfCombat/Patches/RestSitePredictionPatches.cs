using HarmonyLib;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes.RestSite;
using RandomForeseer.RandomForeseerCode.Common.HoverTips;

namespace RandomForeseer.RandomForeseerCode.OutOfCombat.Patches;

[HarmonyPatch(typeof(NRestSiteButton))]
internal static class RestSitePredictionPatches
{
    [HarmonyPatch("OnFocus")]
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void OnFocusPostfix(NRestSiteButton __instance)
    {
        PredictionHoverTipSetHelper.EnsureHoverTipSet(__instance, HoverTip.GetHoverTipAlignment(__instance));
    }

    [HarmonyPatch("OnUnfocus")]
    [HarmonyPostfix]
    private static void OnUnfocusPostfix(NRestSiteButton __instance)
    {
        PredictionHoverTipSetHelper.RemoveOwnedHoverTipSet(__instance);
    }
}
