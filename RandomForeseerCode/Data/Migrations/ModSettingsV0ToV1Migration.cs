using System.Text.Json.Nodes;
using STS2RitsuLib.Utils.Persistence.Migration;

namespace RandomForeseer.RandomForeseerCode.Data.Migrations;

internal sealed class ModSettingsV0ToV1Migration : IMigration
{
    private static readonly string[] LegacyCardPlayFeatureProperties =
    [
        "EnableCombatCardPrediction",
        "EnableCardDrawPrediction",
        "EnableCombatCardSelectionPrediction",
        "EnableOrbPrediction",
        "EnablePotionGenerationPrediction",
        "EnableAutoPlayFromDrawPilePrediction",
        "EnableCombatDamagePrediction",
    ];

    private static readonly string[] LegacyPotionFeatureProperties =
    [
        "EnableCombatCardSelectionPrediction",
        "EnableOrbPrediction",
        "EnablePotionCardPrediction",
        "EnablePotionDrawPrediction",
        "EnablePotionGenerationPrediction",
        "EnableAutoPlayFromDrawPilePrediction",
        "EnableCombatDamagePrediction",
    ];

    private static readonly (string LegacyName, string CurrentName)[] RenamedProperties =
    [
        ("EnableSingleplayerPrediction", nameof(ModSettings.SingleplayerPredictionEnabled)),
        ("EnableMultiplayerPrediction", nameof(ModSettings.MultiplayerPredictionEnabled)),
        ("EnableFairMode", nameof(ModSettings.FairModeEnabled)),
        ("EnableDriftWarnings", nameof(ModSettings.ShowDriftWarnings)),
        ("EnableTransformPrediction", nameof(ModSettings.DeckTransformPredictionEnabled)),
        ("EnableRelicPickupPrediction", nameof(ModSettings.RelicPickupPredictionEnabled)),
        ("EnableEventOptionPrediction", nameof(ModSettings.EventOptionPredictionEnabled)),
        ("EnableCrystalSphereClairvoyance", nameof(ModSettings.CrystalSphereClairvoyanceEnabled)),
        ("EnableDriftwoodRerollPrediction", nameof(ModSettings.DriftwoodRerollPredictionEnabled)),
        ("EnablePaelsWingSacrificePrediction", nameof(ModSettings.PaelsWingSacrificePredictionEnabled)),
        ("EnableRestSitePrediction", nameof(ModSettings.RestSitePredictionEnabled)),
        ("EnableNextActPrediction", nameof(ModSettings.NextActPredictionEnabled)),
        ("EnableCombatTransformPrediction", nameof(ModSettings.CombatTransformPredictionEnabled)),
        ("EnableEndTurnPrediction", nameof(ModSettings.EndTurnPredictionEnabled)),
        ("EnableFrozenEye", nameof(ModSettings.FrozenEyeEnabled)),
        ("EnableShufflePrediction", nameof(ModSettings.ShufflePredictionEnabled)),
        ("EnableCombatCardPrediction", nameof(ModSettings.CombatCardGenerationPredictionEnabled)),
        ("EnableCardDrawPrediction", nameof(ModSettings.CardDrawPredictionEnabled)),
        ("EnableCombatCardSelectionPrediction", nameof(ModSettings.CombatCardSelectionPredictionEnabled)),
        ("EnablePotionCardPrediction", nameof(ModSettings.PotionCardGenerationPredictionEnabled)),
        ("EnablePotionDrawPrediction", nameof(ModSettings.PotionDrawPredictionEnabled)),
        ("EnablePotionGenerationPrediction", nameof(ModSettings.PotionGenerationPredictionEnabled)),
        ("EnableAutoPlayFromDrawPilePrediction", nameof(ModSettings.AutoPlayFromDrawPilePredictionEnabled)),
        ("EnableCombatDamagePrediction", nameof(ModSettings.CombatDamagePredictionEnabled)),
        ("EnableRandomTargetAttackPrediction", nameof(ModSettings.RandomTargetAttackPredictionEnabled)),
        ("ShowDebugSettingsPage", nameof(ModSettings.DebugSettingsEnabled)),
    ];

    public int FromVersion => 0;

    public int ToVersion => 1;

    public bool Migrate(JsonObject data)
    {
        var cardPlayPredictionEnabled = HasAnyLegacyFeatureEnabled(data, LegacyCardPlayFeatureProperties);
        var potionPredictionEnabled = HasAnyLegacyFeatureEnabled(data, LegacyPotionFeatureProperties);

        foreach (var (legacyName, currentName) in RenamedProperties)
        {
            data.MoveProperty(legacyName, currentName);
        }

        SplitOrbPredictionSetting(data);

        data.SetIfMissing(nameof(ModSettings.CardPlayPredictionEnabled), cardPlayPredictionEnabled);
        data.SetIfMissing(nameof(ModSettings.PotionPredictionEnabled), potionPredictionEnabled);

        return true;
    }

    private static bool HasAnyLegacyFeatureEnabled(JsonObject data, IEnumerable<string> propertyNames)
    {
        return propertyNames.Any(propertyName => data.GetBoolean(propertyName) ?? true);
    }

    private static void SplitOrbPredictionSetting(JsonObject data)
    {
        const string legacyName = "EnableOrbPrediction";
        if (data.TryGetPropertyValue(legacyName, out var value))
        {
            data.SetIfMissing(nameof(ModSettings.CombatOrbGenerationPredictionEnabled), value);
            data.SetIfMissing(nameof(ModSettings.OrbDamagePredictionEnabled), value);
            data.Remove(legacyName);
        }
    }
}
