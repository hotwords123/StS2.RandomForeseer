using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Events;

namespace RandomForeseer.OutOfCombat.Events;

internal static class DenseVegetationPrediction
{
    public static IReadOnlyList<IHoverTip> GetHoverTips(DenseVegetation denseVegetation, EventOption option)
    {
        return option.TextKey == "DENSE_VEGETATION.pages.INITIAL.options.REST"
            ? RestSitePrediction.PredictRestTips(denseVegetation.Owner!)
            : [];
    }
}
