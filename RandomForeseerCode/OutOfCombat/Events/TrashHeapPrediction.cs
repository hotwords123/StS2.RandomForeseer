using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Events;
using RandomForeseer.RandomForeseerCode.Common;

namespace RandomForeseer.RandomForeseerCode.OutOfCombat.Events;

internal static class TrashHeapPrediction
{
    public static IReadOnlyList<IHoverTip> GetHoverTips(TrashHeap trashHeap, EventOption option)
    {
        var rng = PredictionUtils.CloneRng(trashHeap.Rng);
        return option.TextKey switch
        {
            "TRASH_HEAP.pages.INITIAL.options.DIVE_IN" =>
                OutOfCombatPredictionUtils.RelicTipsWithPickup(trashHeap.Owner!, [rng.NextItem(TrashHeap.Relics)!]),
            "TRASH_HEAP.pages.INITIAL.options.GRAB" =>
                PredictionHoverTips.Cards([rng.NextItem(TrashHeap.Cards)!]),
            _ => []
        };
    }
}
