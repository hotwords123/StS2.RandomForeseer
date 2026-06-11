using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using RandomForeseer.InCombat;

namespace RandomForeseer.Common;

internal static class PotionPrediction
{
    private static readonly PredictionHoverTipRegistry<PotionModel> PredictionProviders = new();

    static PotionPrediction()
    {
        PredictionProviders.Register("potion card generation", CombatCardGenerationPrediction.GetPotionHoverTips);
        PredictionProviders.Register("potion generation", PotionGenerationPrediction.GetPotionHoverTips);
        PredictionProviders.Register("potion auto-play from draw pile", AutoPlayFromDrawPilePrediction.GetPotionHoverTips);
        PredictionProviders.Register("potion draw", PotionDrawPrediction.GetPotionHoverTips);
    }

    public static IReadOnlyList<IHoverTip> GetHoverTips(PotionModel potion)
    {
        return PredictionProviders.GetHoverTips(potion);
    }

    public static IReadOnlyList<IHoverTip> GetHoverTips(Player player, PotionModel potion)
    {
        var previewPotion = PredictionUtils.CreatePotion(potion, player);
        return PredictionProviders.GetHoverTips(previewPotion);
    }
}
