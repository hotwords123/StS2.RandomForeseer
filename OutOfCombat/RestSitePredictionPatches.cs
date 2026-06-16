using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes.RestSite;
using RandomForeseer.Common;

namespace RandomForeseer.OutOfCombat;

internal static class RestSiteHoverTips
{
    public static IReadOnlyList<IHoverTip> GetHoverTips(Control owner)
    {
        if (!RandomForeseerSettings.IsPredictionFeatureEnabled(RandomForeseerSettings.EnableRestSitePrediction) ||
            owner is not NRestSiteButton button)
        {
            return [];
        }

        return RestSitePrediction.GetHoverTips(button.Option);
    }
}

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
