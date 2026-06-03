using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.RestSite;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using RandomForeseer.Common;

namespace RandomForeseer.OutOfCombat;

[HarmonyPatch(typeof(NRestSiteButton), "OnFocus")]
internal static class RestSitePredictionFocusPatch
{
    private static void Postfix(NRestSiteButton __instance)
    {
        if (!RandomForeseerSettings.IsPredictionFeatureEnabled(RandomForeseerSettings.EnableRestSitePrediction))
        {
            return;
        }

        try
        {
            var tips = GetHoverTips(__instance.Option);
            if (tips.Count <= 0)
            {
                return;
            }

            NHoverTipSet.Remove(__instance);
            NHoverTipSet.CreateAndShow(__instance, tips, HoverTip.GetHoverTipAlignment(__instance));
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"Rest site prediction failed: {ex}");
        }
    }

    private static IReadOnlyList<IHoverTip> GetHoverTips(RestSiteOption option)
    {
        return option switch
        {
            DigRestSiteOption => PredictDigTips(option.Owner),
            HealRestSiteOption => PredictRestTips(option.Owner),
            _ => []
        };
    }

    private static IReadOnlyList<IHoverTip> PredictDigTips(Player player)
    {
        var relics = OutOfCombatPredictionUtils.PredictRelicRewards(player, 1);
        return PredictionHoverTips.Relics(relics);
    }

    private static IReadOnlyList<IHoverTip> PredictRestTips(Player player)
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
                    var cards = OutOfCombatPredictionUtils.PredictCards(
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
                    var potions = PredictPotionRewards(player, 2, rewardRng);
                    tips.AddRange(PredictionHoverTips.Potions(potions));
                    break;
                }
            }
        }

        return tips;
    }

    private static IReadOnlyList<PotionModel> PredictPotionRewards(Player player, int count, Rng rewardRng)
    {
        return Enumerable.Range(0, count)
            .Select(_ => PotionFactory.CreateRandomPotionOutOfCombat(player, rewardRng).ToMutable())
            .ToList();
    }
}

[HarmonyPatch(typeof(NRestSiteButton), "OnUnfocus")]
internal static class RestSitePredictionUnfocusPatch
{
    private static void Postfix(NRestSiteButton __instance)
    {
        HidePrediction(__instance);
    }

    internal static void HidePrediction(NRestSiteButton button)
    {
        NHoverTipSet.Remove(button);
    }
}

[HarmonyPatch(typeof(NRestSiteButton), nameof(NRestSiteButton._ExitTree))]
internal static class RestSitePredictionExitTreePatch
{
    private static void Postfix(NRestSiteButton __instance)
    {
        RestSitePredictionUnfocusPatch.HidePrediction(__instance);
    }
}
