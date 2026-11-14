using Godot;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using RandomForeseer.Common;

namespace RandomForeseer.OutOfCombat;

internal static class MerchantEntryHoverTips
{
    public static IReadOnlyList<IHoverTip> GetHoverTips(Control owner)
    {
        return owner switch
        {
            NMerchantCard { Entry: MerchantCardEntry cardEntry } =>
                MerchantRestockPrediction.GetHoverTips(cardEntry),

            NMerchantRelic { Entry: MerchantRelicEntry { Model: { } relic } relicEntry } =>
            [
                .. RelicPickupPrediction.GetHoverTips(relicEntry._player, relic),
                .. MerchantRestockPrediction.GetHoverTips(relicEntry)
            ],

            NMerchantPotion { Entry: MerchantPotionEntry { Model: { } potion } potionEntry } =>
            [
                .. PotionPrediction.GetHoverTips(potionEntry._player, potion),
                .. MerchantRestockPrediction.GetHoverTips(potionEntry)
            ],

            NBlitzMerchantSlot { Entry: BlitzMerchantEntry blitzEntry } =>
                BlitzMerchantPrediction.GetHoverTips(blitzEntry),

            _ => []
        };
    }
}
