using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Events;
using RandomForeseer.Common;
using RandomForeseer.Common.HoverTips;

namespace RandomForeseer.OutOfCombat.Events;

internal static class TrashHeapPrediction
{
    public static IReadOnlyList<IHoverTip> GetHoverTips(TrashHeap trashHeap, EventOption option)
    {
        var rng = trashHeap.Rng.Clone();
        return option.TextKey switch
        {
            "TRASH_HEAP.pages.INITIAL.options.DIVE_IN" =>
                OutOfCombatPredictionUtils.RelicTipsWithPickup(trashHeap.Owner!, [rng.NextItem(TrashHeap.Relics)!]),
            "TRASH_HEAP.pages.INITIAL.options.GRAB" =>
                [PredictionHoverTipFactory.Card(rng.NextItem(TrashHeap.Cards)!)],
            _ => []
        };
    }
}
