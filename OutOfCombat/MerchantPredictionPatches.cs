using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using RandomForeseer.Common;
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
                RelicPickupPrediction.GetHoverTips(relicEntry._player, relic),

            NMerchantPotion { Entry: MerchantPotionEntry { Model: { } potion } potionEntry } =>
                GetPotionHoverTips(potionEntry._player, potion),

            _ => []
        };

        if (predictionTips.Count > 0)
        {
            hoverTips = hoverTips.Concat(predictionTips).ToList();
        }
    }

    private static IReadOnlyList<IHoverTip> GetPotionHoverTips(Player player, PotionModel potion)
    {
        var previewPotion = PredictionUtils.CreatePotion(potion, player);
        var tips = new List<IHoverTip>();

        try
        {
            tips.AddRange(CombatCardGenerationPrediction.GetPotionHoverTips(previewPotion));
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"Merchant potion combat card generation prediction failed for {potion.Id}: {ex}");
        }

        try
        {
            tips.AddRange(PotionGenerationPrediction.GetPotionHoverTips(previewPotion));
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"Merchant potion generation prediction failed for {potion.Id}: {ex}");
        }

        return tips;
    }
}
