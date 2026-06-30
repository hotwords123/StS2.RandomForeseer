using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using RandomForeseer.RandomForeseerCode.InCombat;
using RandomForeseer.RandomForeseerCode.Integrations.LemonSpire;
using RandomForeseer.RandomForeseerCode.OutOfCombat;

namespace RandomForeseer.RandomForeseerCode.Common;

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

    private static void Prefix(Control owner, ref IEnumerable<IHoverTip> hoverTips)
    {
        var predictionTips = Registry.GetHoverTips(owner);
        if (predictionTips.Count > 0)
        {
            hoverTips = hoverTips.Concat(predictionTips);
        }
    }
}

internal static class PredictionHoverTipSetHelper
{
    private static readonly ConditionalWeakTable<Control, NHoverTipSet> OwnedHoverTips = [];

    public static NHoverTipSet? EnsureHoverTipSet(Control owner, HoverTipAlignment alignment = HoverTipAlignment.None)
    {
        if (NHoverTipSet._activeHoverTips.ContainsKey(owner))
        {
            return null;
        }

        var tipSet = NHoverTipSet.CreateAndShow(owner, [], alignment);
        if (tipSet == null)
        {
            return null;
        }

        OwnedHoverTips.AddOrUpdate(owner, tipSet);
        return tipSet;
    }

    public static void RemoveOwnedHoverTipSet(Control owner)
    {
        if (!OwnedHoverTips.TryGetValue(owner, out var tipSet))
        {
            return;
        }

        OwnedHoverTips.Remove(owner);

        if (NHoverTipSet._activeHoverTips.TryGetValue(owner, out var activeTipSet) &&
            ReferenceEquals(activeTipSet, tipSet))
        {
            NHoverTipSet.Remove(owner);
        }
    }
}
