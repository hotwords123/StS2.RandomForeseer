using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using RandomForeseer.InCombat;

namespace RandomForeseer.OutOfCombat;

[HarmonyPatch(
    typeof(NHoverTipSet),
    nameof(NHoverTipSet.CreateAndShow),
    [typeof(Control), typeof(IEnumerable<IHoverTip>), typeof(HoverTipAlignment)])]
internal static class MerchantPredictionPatch
{
    private static void Prefix(Control owner, ref IEnumerable<IHoverTip> hoverTips)
    {
        IReadOnlyList<IHoverTip> predictionTips = owner switch
        {
            NMerchantRelic { Entry: MerchantRelicEntry { Model: { } relic } relicEntry } =>
                OutOfCombatRelicPrediction.GetHoverTips(relicEntry._player, relic),

            NMerchantPotion { Entry: MerchantPotionEntry { Model: { } potion } potionEntry } =>
                PotionGenerationPrediction.GetPotionHoverTips(potionEntry._player, potion),

            _ => []
        };

        if (predictionTips.Count > 0)
        {
            hoverTips = hoverTips.Concat(predictionTips).ToList();
        }
    }
}
