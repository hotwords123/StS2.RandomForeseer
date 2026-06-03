using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.RestSite;
using RandomForeseer.Common;

namespace RandomForeseer.OutOfCombat;

[HarmonyPatch(typeof(NRestSiteButton), "OnFocus")]
internal static class RestSiteDigPredictionFocusPatch
{
    private static void Postfix(NRestSiteButton __instance)
    {
        if (__instance.Option is not DigRestSiteOption option ||
            !RandomForeseerSettings.IsPredictionFeatureEnabled(RandomForeseerSettings.EnableRestSiteDigPrediction))
        {
            return;
        }

        try
        {
            NHoverTipSet.Remove(__instance);
            var relics = OutOfCombatPredictionUtils.PredictRelicRewards(option.Owner, 1);
            var tips = PredictionHoverTips.Relics(relics);
            if (tips.Count > 0)
            {
                NHoverTipSet.CreateAndShow(__instance, tips, GetSideAlignment(__instance));
            }
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"Rest site Dig prediction failed: {ex}");
        }
    }

    private static HoverTipAlignment GetSideAlignment(Control button)
    {
        var viewportWidth = button.GetViewport().GetVisibleRect().Size.X;
        var buttonCenterX = button.GlobalPosition.X + button.Size.X * button.Scale.X / 2f;

        return buttonCenterX < viewportWidth / 2f
            ? HoverTipAlignment.Right
            : HoverTipAlignment.Left;
    }
}

[HarmonyPatch(typeof(NRestSiteButton), "OnUnfocus")]
internal static class RestSiteDigPredictionUnfocusPatch
{
    private static void Postfix(NRestSiteButton __instance)
    {
        HidePrediction(__instance);
    }

    internal static void HidePrediction(NRestSiteButton button)
    {
        NHoverTipSet.Remove(button);
    }
}

[HarmonyPatch(typeof(NRestSiteButton), nameof(NRestSiteButton._ExitTree))]
internal static class RestSiteDigPredictionExitTreePatch
{
    private static void Postfix(NRestSiteButton __instance)
    {
        RestSiteDigPredictionUnfocusPatch.HidePrediction(__instance);
    }
}
