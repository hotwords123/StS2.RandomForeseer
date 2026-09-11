using System.Text.Json.Serialization;
using STS2RitsuLib.Utils.Persistence.Migration;

namespace RandomForeseer.RandomForeseerCode.Data;

internal sealed class ModSettings
{
    public const int CurrentSchemaVersion = 3;

    public static ModSettings Default { get; } = new();

    [JsonPropertyName(ModDataVersion.SchemaVersionProperty)]
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    // General settings
    public bool SingleplayerPredictionEnabled { get; set; } = true;
    public bool MultiplayerPredictionEnabled { get; set; } = true;
    public bool FairModeEnabled { get; set; } = true;
    public bool ShowDriftWarnings { get; set; }

    // Compatibility settings
    public bool PredictBaseGameCardsOnly { get; set; }
    public bool InvokeBaseGameHookListenersOnly { get; set; }

    // Out-of-combat prediction settings
    public bool DeckTransformPredictionEnabled { get; set; } = true;
    public bool NextActPredictionEnabled { get; set; } = true;

    public bool EventOptionPredictionEnabled { get; set; } = true;
    public int SlipperyBridgeRerollPreviewCount
    {
        get => Math.Clamp(field, 1, 10);
        set => field = Math.Clamp(value, 1, 10);
    } = 5;
    public bool CrystalSphereClairvoyanceEnabled { get; set; } = true;

    public bool RelicPickupPredictionEnabled { get; set; } = true;
    public bool AncientRelicPickupPredictionEnabled { get; set; } = true;
    public bool MerchantRestockPredictionEnabled { get; set; } = true;
    public bool DriftwoodRerollPredictionEnabled { get; set; } = true;
    public bool PaelsWingSacrificePredictionEnabled { get; set; } = true;
    public bool RestSitePredictionEnabled { get; set; } = true;

    // In-combat prediction triggers
    public bool CardPlayPredictionEnabled { get; set; } = true;
    public bool PotionPredictionEnabled { get; set; } = true;
    public bool CombatTransformPredictionEnabled { get; set; } = true;

    public bool EndTurnPredictionEnabled { get; set; } = true;
    public EndTurnPredictionDisplayMode EndTurnPredictionDisplayMode { get; set; } =
        EndTurnPredictionDisplayMode.EndTurnButtonHover;
    public EndTurnPredictionDisplayMode EndTurnHealthBarForecastDisplayMode { get; set; } =
        EndTurnPredictionDisplayMode.AlwaysDuringPlayerTurn;

    public bool FrozenEyeEnabled { get; set; } = true;
    public bool ShufflePredictionEnabled { get; set; } = true;

    // Card effect prediction
    public bool CombatCardGenerationPredictionEnabled { get; set; } = true;
    public bool CardDrawPredictionEnabled { get; set; } = true;
    public bool CombatCardSelectionPredictionEnabled { get; set; } = true;
    public bool CombatOrbGenerationPredictionEnabled { get; set; } = true;

    // Potion effect prediction
    public bool PotionCardGenerationPredictionEnabled { get; set; } = true;
    public bool PotionDrawPredictionEnabled { get; set; } = true;

    // Shared effect prediction
    public bool PotionGenerationPredictionEnabled { get; set; } = true;
    public bool AutoPlayFromDrawPilePredictionEnabled { get; set; } = true;

    // Card resolution prediction
    public bool InferCardOnPlayEffectsEnabled { get; set; } = true;
    public bool ChainedCardEffectPredictionEnabled { get; set; } = true;

    // Damage prediction
    public bool CombatDamagePredictionEnabled { get; set; } = true;
    public bool OrbDamagePredictionEnabled { get; set; } = true;
    public bool RandomTargetAttackPredictionEnabled { get; set; } = true;
    public string DamagePredictionHealthBarColor { get; set; } = "#E8C91A";

    // Debug settings
    public bool DebugSettingsEnabled { get; set; }
    public bool EventDebugRerollEnabled { get; set; }
}

internal enum PredictionFairness
{
    Fair,
    UnfairInSingleplayer,
    UnfairInAllModes
}

internal enum EndTurnPredictionDisplayMode
{
    EndTurnButtonHover,
    AlwaysDuringPlayerTurn
}
