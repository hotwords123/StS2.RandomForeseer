using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using RandomForeseer.InCombat;

namespace RandomForeseer.Common;

internal static class PotionPrediction
{
    public static IReadOnlyList<IHoverTip> GetHoverTips(Player player, PotionModel potion)
    {
        var previewPotion = PredictionUtils.CreatePotion(potion, player);
        var tips = new List<IHoverTip>();

        try
        {
            tips.AddRange(CombatCardGenerationPrediction.GetPotionHoverTips(previewPotion));
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"Potion card prediction failed for {potion.Id}: {ex}");
        }

        try
        {
            tips.AddRange(PotionGenerationPrediction.GetPotionHoverTips(previewPotion));
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"Potion generation prediction failed for {potion.Id}: {ex}");
        }

        try
        {
            tips.AddRange(PotionDrawPrediction.GetPotionHoverTips(previewPotion));
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"Potion draw-pile prediction failed for {potion.Id}: {ex}");
        }

        return tips;
    }
}
