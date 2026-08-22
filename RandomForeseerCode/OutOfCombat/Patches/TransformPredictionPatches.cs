using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;

namespace RandomForeseer.RandomForeseerCode.OutOfCombat.Patches;

[HarmonyPatch(typeof(NDeckTransformSelectScreen), "OnCardClicked")]
internal static class TransformSelectionHoverTipRefreshPatch
{
    private static void Postfix(NDeckTransformSelectScreen __instance, CardModel card)
    {
        var holder = __instance._grid.GetCardHolder(card);
        if (holder is not { _isHovered: true })
        {
            return;
        }

        NHoverTipSet.Remove(holder);
        if (!__instance._previewContainer.Visible)
        {
            holder.Call(NCardHolder.MethodName.CreateHoverTips);
        }
    }
}
