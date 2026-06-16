using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using RandomForeseer.InCombat;
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
        Registry.Register("rest site", RestSiteHoverTips.GetHoverTips);
        Registry.Register("combat transform selected holder", CombatTransformSelectedHoverTips.GetHoverTips);
        Registry.Register("card reward reroll", CardRewardRerollButtonHoverTips.GetHoverTips);
        Registry.Register("lemonSpire", LemonSpireControlHoverTips.GetHoverTips);
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
