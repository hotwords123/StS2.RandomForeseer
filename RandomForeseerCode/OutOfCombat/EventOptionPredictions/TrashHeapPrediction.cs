using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Events;

namespace RandomForeseer;

internal static class TrashHeapPrediction
{
    public static void Register()
    {
        EventOptionPredictionRegistry.Register<TrashHeap>(GetHoverTips);
    }

    private static IReadOnlyList<IHoverTip> GetHoverTips(TrashHeap trashHeap, EventOption option)
    {
        var rng = PredictionUtils.CloneRng(trashHeap.Rng);
        return option.TextKey switch
        {
            "TRASH_HEAP.pages.INITIAL.options.DIVE_IN" =>
                OutOfCombatPredictionUtils.RelicTipsWithPickup(trashHeap.Owner!, [rng.NextItem(TrashHeap.Relics)!.ToMutable()]),
            "TRASH_HEAP.pages.INITIAL.options.GRAB" =>
                PredictionHoverTips.Cards([
                    PredictionUtils.CreatePreviewCard(
                        rng.NextItem(TrashHeap.Cards)!,
                        trashHeap.Owner!)
                ]),
            _ => []
        };
    }
}
