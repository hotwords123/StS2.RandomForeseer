using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using RandomForeseer.RandomForeseerCode.Common.HoverTips;
using RandomForeseer.RandomForeseerCode.InCombat;
using RandomForeseer.RandomForeseerCode.Integrations.LemonSpire;
using RandomForeseer.RandomForeseerCode.OutOfCombat;

namespace RandomForeseer.RandomForeseerCode.Common.Patches;

/// <summary>
/// Appends feature-specific prediction tips when vanilla creates a control's HoverTip set.
/// </summary>
/// <remarks>
/// This patch only injects provider results after the supplied vanilla sequence. It does not create or own the
/// HoverTip set; <see cref="PredictionHoverTipSetHelper"/> is used by explicit prediction-only surfaces for that.
/// </remarks>
[HarmonyPatch(
    typeof(NHoverTipSet),
    nameof(NHoverTipSet.CreateAndShow),
    [typeof(Control), typeof(IEnumerable<IHoverTip>), typeof(HoverTipAlignment)])]
internal static class ControlHoverTipPredictionPatch
{
    private static readonly PredictionHoverTipRegistry<Control> Registry = CreateRegistry();

    private static PredictionHoverTipRegistry<Control> CreateRegistry()
    {
        var registry = new PredictionHoverTipRegistry<Control>();

        registry.Register("merchant entry", MerchantEntryHoverTips.GetHoverTips);
        registry.Register("transform selection", TransformSelectionHoverTips.GetHoverTips);
        registry.Register("treasure room relic", TreasureRoomRelicHoverTips.GetHoverTips);
        registry.Register("rest site", RestSiteHoverTips.GetHoverTips);
        registry.Register("combat transform selected holder", CombatTransformSelectedHoverTips.GetHoverTips);
        registry.Register("card reward alternative", CardRewardAlternativeButtonHoverTips.GetHoverTips);
        registry.Register("lemonSpire", LemonSpireControlHoverTips.GetHoverTips);

        return registry;
    }

    /// <summary>
    /// Appends all successful providers registered for the hovered control to vanilla tips.
    /// </summary>
    /// <remarks>
    /// The registry handles provider failures independently. Existing vanilla tips remain the prefix of the resulting
    /// sequence, preserving their order.
    /// </remarks>
    private static void Prefix(Control owner, ref IEnumerable<IHoverTip> hoverTips)
    {
        var predictionTips = Registry.GetHoverTips(owner);
        if (predictionTips.Count > 0)
        {
            hoverTips = hoverTips.Concat(predictionTips);
        }
    }
}
