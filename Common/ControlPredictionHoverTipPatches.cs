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
internal static class ControlPredictionHoverTipPatches
{
    private static readonly PredictionHoverTipRegistry<Control> Registry = new();

    static ControlPredictionHoverTipPatches()
    {
        Registry.Register("event option", EventOptionPrediction.GetHoverTips);
        Registry.Register("merchant", MerchantPrediction.GetHoverTips);
        Registry.Register("transform selection", TransformSelectionPrediction.GetHoverTips);
        Registry.Register("treasure room relic", TreasureRoomRelicPrediction.GetHoverTips);
        Registry.Register("lemonSpire", LemonSpirePredictionControls.GetHoverTips);
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
