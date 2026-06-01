using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Potions;

namespace RandomForeseer;

[HarmonyPatch(
    typeof(NHoverTipSet),
    nameof(NHoverTipSet.CreateAndShow),
    [typeof(Control), typeof(IEnumerable<IHoverTip>), typeof(HoverTipAlignment)])]
internal static class PotionPopupPredictionPatches
{
    private static void Prefix(Control owner, ref IEnumerable<IHoverTip> hoverTips)
    {
        if (!IsInsidePotionPopup(owner))
        {
            return;
        }

        // The potion popup deliberately creates its own fixed text hover tip before blocking new hover tips.
        // Prediction cards are useful on pre-click hover, but they should not be part of that popup tooltip.
        hoverTips = hoverTips.Where(tip => tip is not PredictionCardHoverTip).ToList();
    }

    private static bool IsInsidePotionPopup(Node node)
    {
        for (var current = node; current != null; current = current.GetParent())
        {
            if (current is NPotionPopup)
            {
                return true;
            }
        }

        return false;
    }
}
