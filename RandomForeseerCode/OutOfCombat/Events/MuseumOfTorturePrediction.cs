using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Events;
using RandomForeseer.Common.HoverTips;

namespace RandomForeseer.OutOfCombat.Events;

internal static class MuseumOfTorturePrediction
{
    public static IReadOnlyList<IHoverTip> GetHoverTips(MuseumOfTorture museum, EventOption option)
    {
        if (option.TextKey != "MUSEUM_OF_TORTURE.pages.DISPLAY.options.TAKE")
        {
            return [];
        }

        var context = new RunPredictionContext(museum.Owner!);
        RelicPickupPrediction.FastForwardRelicPickup(context, museum._offeredRelic);
        return [.. OutOfCombatPredictionUtils.PredictCurses(context, 1).ToPredictionHoverTips()];
    }
}
