using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Potions;
using RandomForeseer.RandomForeseerCode.Common.HoverTips;
using RandomForeseer.RandomForeseerCode.Data;
using RandomForeseer.RandomForeseerCode.InCombat;
using RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Potions.OnUse;
using RandomForeseer.RandomForeseerCode.Telemetry;

namespace RandomForeseer.RandomForeseerCode.Common;

internal static class PotionPrediction
{
    public static IReadOnlyList<IHoverTip> GetHoverTips(PotionModel potion)
    {
        var settings = ModData.Settings;
        if (!settings.IsPredictionEnabled || !settings.PotionPredictionEnabled)
        {
            return [];
        }

        if (CombatManager.Instance.IsInProgress && potion.Owner.Creature.CombatState is not null)
        {
            return CombatPotionPrediction.GetHoverTips(potion);
        }

        try
        {
            return GetOutOfCombatHoverTips(potion, potion.Owner);
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"Out-of-combat potion prediction failed for {potion.Id}: {ex}");
            ModTelemetry.CaptureException(
                ex,
                "potion_prediction",
                "build_out_of_combat_hover_tips",
                TelemetryContext.ForModel(potion));
            return [];
        }
    }

    public static IReadOnlyList<IHoverTip> GetHoverTips(Player player, PotionModel potion)
    {
        return GetHoverTips(PredictionUtils.CreatePotion(potion, player));
    }

    /// <summary>Builds only the pure RNG previews supported outside combat.</summary>
    private static List<IHoverTip> GetOutOfCombatHoverTips(PotionModel potion, Player target)
    {
        List<IHoverTip> hoverTips = [];

        var settings = ModData.Settings;

        if (settings.PotionCardGenerationPredictionEnabled && settings.Allows(PredictionFairness.UnfairInAllModes))
        {
            var rng = target.RunState.Rng.CombatCardGeneration.Clone();
            if (CardGenerationPotionMirrors.Generate(potion, target, rng) is { } result)
            {
                hoverTips.AddRange(result.Cards.SelectPreviews().ToPredictionHoverTips());
            }
        }

        if (settings.PotionGenerationPredictionEnabled && potion is EntropicBrew)
        {
            var rng = target.RunState.Rng.CombatPotionGeneration.Clone();
            hoverTips.AddRange(EntropicBrewMirrors.Generate(target, rng).ToPredictionHoverTips());
        }

        return hoverTips;
    }
}
