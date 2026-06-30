using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Events;
using RandomForeseer.RandomForeseerCode.Common;

namespace RandomForeseer.RandomForeseerCode.OutOfCombat.Events;

internal static class PotionCourierPrediction
{
    public static IReadOnlyList<IHoverTip> GetHoverTips(PotionCourier potionCourier, EventOption option)
    {
        var player = potionCourier.Owner!;
        return option.TextKey == "POTION_COURIER.pages.INITIAL.options.RANSACK"
            ? PredictionHoverTips.Potions(OutOfCombatPredictionUtils.PredictUniformPotions(
                player,
                1,
                filter: potion => potion.Rarity == PotionRarity.Uncommon))
            : [];
    }
}
