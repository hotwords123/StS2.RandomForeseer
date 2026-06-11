using Godot;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using RandomForeseer.Common;

namespace RandomForeseer.OutOfCombat;

internal static class MerchantPrediction
{
    public static IReadOnlyList<IHoverTip> GetHoverTips(Control owner)
    {
        return owner switch
        {
            NMerchantRelic { Entry: MerchantRelicEntry { Model: { } relic } relicEntry } =>
                RelicPickupPrediction.GetHoverTips(relicEntry._player, relic),

            NMerchantPotion { Entry: MerchantPotionEntry { Model: { } potion } potionEntry } =>
                PotionPrediction.GetHoverTips(potionEntry._player, potion),

            _ => []
        };
    }
}
