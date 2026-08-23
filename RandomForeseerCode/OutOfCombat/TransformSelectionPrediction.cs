using Godot;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;

namespace RandomForeseer.RandomForeseerCode.OutOfCombat;

internal static class TransformSelectionHoverTips
{
    public static IReadOnlyList<IHoverTip> GetHoverTips(Control owner)
    {
        if (owner is not NGridCardHolder holder ||
            FindTransformSelectScreen(holder) is not { } screen ||
            screen._cardToTransformation.Target is not DeckTransformPreviewPredictor predictor)
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
