using Godot;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes.RestSite;
using RandomForeseer.RandomForeseerCode.Data;

namespace RandomForeseer.RandomForeseerCode.OutOfCombat;

/// <summary>Provides prediction HoverTips for rest-site option controls.</summary>
internal static class RestSiteHoverTips
{
    public static IReadOnlyList<IHoverTip> GetHoverTips(Control owner)
    {
        var settings = ModData.Settings;
        if (!settings.IsPredictionEnabled || !settings.RestSitePredictionEnabled ||
            owner is not NRestSiteButton button)
        {
            return [];
        }

        return RestSitePrediction.GetHoverTips(button.Option);
    }
}
