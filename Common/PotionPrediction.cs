using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using RandomForeseer.InCombat;

namespace RandomForeseer.Common;

internal static class PotionPrediction
{
    private static readonly PredictionHoverTipRegistry<PotionModel> PredictionProviders = CreateRegistry();

    private static PredictionHoverTipRegistry<PotionModel> CreateRegistry()
    {
        var registry = new PredictionHoverTipRegistry<PotionModel>();

        registry.Register("potion card generation", CombatCardGenerationPrediction.GetPotionHoverTips);
        registry.Register("potion generation", PotionGenerationPrediction.GetPotionHoverTips);
        registry.Register("potion auto-play from draw pile", AutoPlayFromDrawPilePrediction.GetPotionHoverTips);
        registry.Register("potion draw", PotionDrawPrediction.GetPotionHoverTips);

        return registry;
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
