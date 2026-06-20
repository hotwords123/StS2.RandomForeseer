using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;

namespace RandomForeseer.OutOfCombat;

internal static class TransformSelectionHoverTips
{
    public static IReadOnlyList<IHoverTip> GetHoverTips(Control owner)
    {
        if (owner is not NGridCardHolder holder ||
            FindTransformSelectScreen(holder) is not { } screen ||
            screen._cardToTransformation.Target is not TransformPreviewPredictor predictor)
        {
            return [];
        }

        return predictor.GetHoverTips(holder.CardModel, screen._selectedCards, screen._prefs.MaxSelect);
    }

    private static NDeckTransformSelectScreen? FindTransformSelectScreen(NCardHolder holder)
    {
        for (var node = holder.GetParent(); node != null; node = node.GetParent())
        {
            if (node is NDeckTransformSelectScreen screen)
            {
                return screen;
            }
        }

        return null;
    }
}

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
