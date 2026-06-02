using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;

namespace RandomForeseer.OutOfCombat;

[HarmonyPatch(
    typeof(NHoverTipSet),
    nameof(NHoverTipSet.CreateAndShow),
    [typeof(Control), typeof(IEnumerable<IHoverTip>), typeof(HoverTipAlignment)])]
internal static class MerchantRelicPredictionPatch
{
    private static readonly AccessTools.FieldRef<MerchantEntry, Player> GetEntryPlayer =
        AccessTools.FieldRefAccess<MerchantEntry, Player>("_player");

    private static void Prefix(Control owner, ref IEnumerable<IHoverTip> hoverTips)
    {
        if (owner is not NMerchantRelic { Entry: MerchantRelicEntry { Model: { } relic } entry })
        {
            return;
        }

        // MerchantRelicEntry.Model is mutable: FillSlot and predetermined slot setup both assert/set mutable models.
        var predictionTips = OutOfCombatRelicPrediction.GetHoverTips(GetEntryPlayer(entry), relic);
        if (predictionTips.Count > 0)
        {
            hoverTips = hoverTips.Concat(predictionTips).ToList();
        }
    }
}
