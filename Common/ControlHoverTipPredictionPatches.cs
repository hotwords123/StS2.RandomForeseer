using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using RandomForeseer.Integrations.LemonSpire;
using RandomForeseer.OutOfCombat;

namespace RandomForeseer.Common;

[HarmonyPatch(
    typeof(NHoverTipSet),
    nameof(NHoverTipSet.CreateAndShow),
    [typeof(Control), typeof(IEnumerable<IHoverTip>), typeof(HoverTipAlignment)])]
internal static class ControlHoverTipPredictionPatch
{
    private static readonly PredictionHoverTipRegistry<Control> Registry = new();

    static ControlHoverTipPredictionPatch()
    {
        Registry.Register("event option", EventOptionHoverTips.GetHoverTips);
        Registry.Register("merchant entry", MerchantEntryHoverTips.GetHoverTips);
        Registry.Register("transform selection", TransformSelectionHoverTips.GetHoverTips);
        Registry.Register("treasure room relic", TreasureRoomRelicHoverTips.GetHoverTips);
        Registry.Register("lemonSpire", LemonSpireControlHoverTips.GetHoverTips);
    }

    private static void Prefix(Control owner, ref IEnumerable<IHoverTip> hoverTips)
    {
        var predictionTips = Registry.GetHoverTips(owner);
        if (predictionTips.Count > 0)
        {
            hoverTips = hoverTips.Concat(predictionTips).ToList();
        }
    }
}
