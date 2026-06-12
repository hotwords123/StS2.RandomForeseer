using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using RandomForeseer.Common;

namespace RandomForeseer.OutOfCombat;

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
        var rewardRng = PredictionUtils.CloneRng(player.PlayerRng.Rewards);
        var nicheRng = PredictionUtils.CloneRng(player.RunState.Rng.Niche);
        var tips = new List<IHoverTip>();

        foreach (var relic in player.Relics.Where(relic => !relic.IsMelted))
        {
            switch (relic)
            {
                case DreamCatcher:
                {
                    var cards = CardRewardPrediction.PredictCards(
                        player,
                        3,
                        CardCreationOptions.ForRoom(player, RoomType.Monster).WithFlags(CardCreationFlags.IsCardReward),
                        rewardRng,
                        nicheRng);
                    tips.AddRange(PredictionHoverTips.Cards(cards));
                    break;
                }
                case TinyMailbox:
                {
                    var potions = PredictionUtils.PredictPotionRewards(player, 2, rewardRng);
                    tips.AddRange(PredictionHoverTips.Potions(potions));
                    break;
                }
            }
        }

        return tips;
    }
}
