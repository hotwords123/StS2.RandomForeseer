using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using RandomForeseer.RandomForeseerCode.Common;

namespace RandomForeseer.RandomForeseerCode.OutOfCombat;

internal static class RestSitePrediction
{
    public static IReadOnlyList<IHoverTip> GetHoverTips(RestSiteOption option)
    {
        return option switch
        {
            DigRestSiteOption => PredictDigTips(option.Owner),
            HealRestSiteOption => PredictRestTips(option.Owner),
            _ => []
        };
    }

    public static IReadOnlyList<IHoverTip> PredictDigTips(Player player)
    {
        var relics = OutOfCombatPredictionUtils.PredictRelicRewards(player, 1);
        return OutOfCombatPredictionUtils.RelicTipsWithPickup(player, relics);
    }

    public static IReadOnlyList<IHoverTip> PredictRestTips(Player player)
    {
        var context = new RunPredictionContext(player);
        var tips = new List<IHoverTip>();

        foreach (var relic in player.Relics.Where(relic => !relic.IsMelted))
        {
            switch (relic)
            {
                case DreamCatcher:
                {
                    var options = CardCreationOptions.ForRoom(player, RoomType.Monster)
                        .WithFlags(CardCreationFlags.IsCardReward);
                    tips.AddRange(PredictionHoverTips.Cards(CardRewardPrediction.PredictCards(context, 3, options)));
                    break;
                }
                case TinyMailbox:
                {
                    var potions = PredictionUtils.PredictPotionRewards(player, 2, context.Rng.Rewards);
                    tips.AddRange(PredictionHoverTips.Potions(potions));
                    break;
                }
            }
        }

        return tips;
    }
}
