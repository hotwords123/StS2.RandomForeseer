using HarmonyLib;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.RestSite;

namespace RandomForeseer.OutOfCombat;

[HarmonyPatch(typeof(NRestSiteButton))]
internal static class RestSitePredictionPatches
{
    [HarmonyPatch("OnFocus")]
    [HarmonyPostfix]
    private static void OnFocusPostfix(NRestSiteButton __instance)
    {
        if (!RandomForeseerSettings.IsPredictionFeatureEnabled(RandomForeseerSettings.EnableRestSitePrediction))
        {
            return;
        }

        try
        {
            var tips = RestSitePrediction.GetHoverTips(__instance.Option);
            if (tips.Count <= 0)
            {
                return;
            }

            NHoverTipSet.Remove(__instance);
            NHoverTipSet.CreateAndShow(__instance, tips, HoverTip.GetHoverTipAlignment(__instance));
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"Rest site prediction failed: {ex}");
        }
    }

    [HarmonyPatch("OnUnfocus")]
    [HarmonyPostfix]
    private static void OnUnfocusPostfix(NRestSiteButton __instance)
    {
        HidePrediction(__instance);
    }

    private static void HidePrediction(NRestSiteButton button)
    {
        NHoverTipSet.Remove(button);
    }
}
