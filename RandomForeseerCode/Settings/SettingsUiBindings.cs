using RandomForeseer.RandomForeseerCode.Data;
using STS2RitsuLib.Settings;

namespace RandomForeseer.RandomForeseerCode.Settings;

internal static class SettingsUiBindings
{
    // General settings
    public static IModSettingsValueBinding<bool> SingleplayerPredictionEnabled { get; } =
        Binding(s => s.SingleplayerPredictionEnabled, (s, v) => s.SingleplayerPredictionEnabled = v);

    public static IModSettingsValueBinding<bool> MultiplayerPredictionEnabled { get; } =
        Binding(s => s.MultiplayerPredictionEnabled, (s, v) => s.MultiplayerPredictionEnabled = v);

    public static IModSettingsValueBinding<bool> FairModeEnabled { get; } =
        Binding(s => s.FairModeEnabled, (s, v) => s.FairModeEnabled = v);

    public static IModSettingsValueBinding<bool> ShowDriftWarnings { get; } =
        Binding(s => s.ShowDriftWarnings, (s, v) => s.ShowDriftWarnings = v);

    // Compatibility settings
    public static IModSettingsValueBinding<bool> PredictBaseGameCardsOnly { get; } =
        Binding(s => s.PredictBaseGameCardsOnly, (s, v) => s.PredictBaseGameCardsOnly = v);

    public static IModSettingsValueBinding<bool> InvokeBaseGameHookListenersOnly { get; } =
        Binding(s => s.InvokeBaseGameHookListenersOnly, (s, v) => s.InvokeBaseGameHookListenersOnly = v);

    // Out-of-combat prediction settings
    public static IModSettingsValueBinding<bool> DeckTransformPredictionEnabled { get; } =
        Binding(s => s.DeckTransformPredictionEnabled, (s, v) => s.DeckTransformPredictionEnabled = v);

    public static IModSettingsValueBinding<bool> NextActPredictionEnabled { get; } =
        Binding(s => s.NextActPredictionEnabled, (s, v) => s.NextActPredictionEnabled = v);

    public static IModSettingsValueBinding<bool> EventOptionPredictionEnabled { get; } =
        Binding(s => s.EventOptionPredictionEnabled, (s, v) => s.EventOptionPredictionEnabled = v);

    public static IModSettingsValueBinding<int> SlipperyBridgeRerollPreviewCount { get; } =
        Binding(s => s.SlipperyBridgeRerollPreviewCount, (s, v) => s.SlipperyBridgeRerollPreviewCount = v);

    public static IModSettingsValueBinding<bool> CrystalSphereClairvoyanceEnabled { get; } =
        Binding(s => s.CrystalSphereClairvoyanceEnabled, (s, v) => s.CrystalSphereClairvoyanceEnabled = v);

    public static IModSettingsValueBinding<bool> RelicPickupPredictionEnabled { get; } =
        Binding(s => s.RelicPickupPredictionEnabled, (s, v) => s.RelicPickupPredictionEnabled = v);

    public static IModSettingsValueBinding<bool> AncientRelicPickupPredictionEnabled { get; } =
        Binding(s => s.AncientRelicPickupPredictionEnabled, (s, v) => s.AncientRelicPickupPredictionEnabled = v);

    public static IModSettingsValueBinding<bool> MerchantRestockPredictionEnabled { get; } =
        Binding(s => s.MerchantRestockPredictionEnabled, (s, v) => s.MerchantRestockPredictionEnabled = v);

    public static IModSettingsValueBinding<bool> DriftwoodRerollPredictionEnabled { get; } =
        Binding(s => s.DriftwoodRerollPredictionEnabled, (s, v) => s.DriftwoodRerollPredictionEnabled = v);

    public static IModSettingsValueBinding<bool> PaelsWingSacrificePredictionEnabled { get; } =
        Binding(s => s.PaelsWingSacrificePredictionEnabled, (s, v) => s.PaelsWingSacrificePredictionEnabled = v);

    public static IModSettingsValueBinding<bool> RestSitePredictionEnabled { get; } =
        Binding(s => s.RestSitePredictionEnabled, (s, v) => s.RestSitePredictionEnabled = v);

    // In-combat prediction triggers
    public static IModSettingsValueBinding<bool> CardPlayPredictionEnabled { get; } =
        Binding(s => s.CardPlayPredictionEnabled, (s, v) => s.CardPlayPredictionEnabled = v);

    public static IModSettingsValueBinding<bool> PotionPredictionEnabled { get; } =
        Binding(s => s.PotionPredictionEnabled, (s, v) => s.PotionPredictionEnabled = v);

    public static IModSettingsValueBinding<bool> CombatTransformPredictionEnabled { get; } =
        Binding(s => s.CombatTransformPredictionEnabled, (s, v) => s.CombatTransformPredictionEnabled = v);

    public static IModSettingsValueBinding<bool> EndTurnPredictionEnabled { get; } =
        Binding(s => s.EndTurnPredictionEnabled, (s, v) => s.EndTurnPredictionEnabled = v);

    public static IModSettingsValueBinding<EndTurnPredictionDisplayMode> EndTurnPredictionDisplayMode { get; } =
        Binding(s => s.EndTurnPredictionDisplayMode, (s, v) => s.EndTurnPredictionDisplayMode = v);

    public static IModSettingsValueBinding<EndTurnPredictionDisplayMode> EndTurnHealthBarForecastDisplayMode { get; } =
        Binding(s => s.EndTurnHealthBarForecastDisplayMode, (s, v) => s.EndTurnHealthBarForecastDisplayMode = v);

    public static IModSettingsValueBinding<bool> FrozenEyeEnabled { get; } =
        Binding(s => s.FrozenEyeEnabled, (s, v) => s.FrozenEyeEnabled = v);

    public static IModSettingsValueBinding<bool> ShufflePredictionEnabled { get; } =
        Binding(s => s.ShufflePredictionEnabled, (s, v) => s.ShufflePredictionEnabled = v);

    // Card effect prediction
    public static IModSettingsValueBinding<bool> CombatCardGenerationPredictionEnabled { get; } =
        Binding(s => s.CombatCardGenerationPredictionEnabled, (s, v) => s.CombatCardGenerationPredictionEnabled = v);

    public static IModSettingsValueBinding<bool> CardDrawPredictionEnabled { get; } =
        Binding(s => s.CardDrawPredictionEnabled, (s, v) => s.CardDrawPredictionEnabled = v);

    public static IModSettingsValueBinding<bool> CombatCardSelectionPredictionEnabled { get; } =
        Binding(s => s.CombatCardSelectionPredictionEnabled, (s, v) => s.CombatCardSelectionPredictionEnabled = v);

    public static IModSettingsValueBinding<bool> CombatOrbGenerationPredictionEnabled { get; } =
        Binding(s => s.CombatOrbGenerationPredictionEnabled, (s, v) => s.CombatOrbGenerationPredictionEnabled = v);

    // Potion effect prediction
    public static IModSettingsValueBinding<bool> PotionCardGenerationPredictionEnabled { get; } =
        Binding(s => s.PotionCardGenerationPredictionEnabled, (s, v) => s.PotionCardGenerationPredictionEnabled = v);

    public static IModSettingsValueBinding<bool> PotionDrawPredictionEnabled { get; } =
        Binding(s => s.PotionDrawPredictionEnabled, (s, v) => s.PotionDrawPredictionEnabled = v);

    // Shared effect prediction
    public static IModSettingsValueBinding<bool> PotionGenerationPredictionEnabled { get; } =
        Binding(s => s.PotionGenerationPredictionEnabled, (s, v) => s.PotionGenerationPredictionEnabled = v);

    public static IModSettingsValueBinding<bool> AutoPlayFromDrawPilePredictionEnabled { get; } =
        Binding(s => s.AutoPlayFromDrawPilePredictionEnabled, (s, v) => s.AutoPlayFromDrawPilePredictionEnabled = v);

    // Card resolution prediction
    public static IModSettingsValueBinding<bool> InferCardOnPlayEffectsEnabled { get; } =
        Binding(s => s.InferCardOnPlayEffectsEnabled, (s, v) => s.InferCardOnPlayEffectsEnabled = v);

    public static IModSettingsValueBinding<bool> ChainedCardEffectPredictionEnabled { get; } =
        Binding(s => s.ChainedCardEffectPredictionEnabled, (s, v) => s.ChainedCardEffectPredictionEnabled = v);

    // Damage prediction
    public static IModSettingsValueBinding<bool> CombatDamagePredictionEnabled { get; } =
        Binding(s => s.CombatDamagePredictionEnabled, (s, v) => s.CombatDamagePredictionEnabled = v);

    public static IModSettingsValueBinding<bool> OrbDamagePredictionEnabled { get; } =
        Binding(s => s.OrbDamagePredictionEnabled, (s, v) => s.OrbDamagePredictionEnabled = v);

    public static IModSettingsValueBinding<bool> RandomTargetAttackPredictionEnabled { get; } =
        Binding(s => s.RandomTargetAttackPredictionEnabled, (s, v) => s.RandomTargetAttackPredictionEnabled = v);

    public static IModSettingsValueBinding<string> DamagePredictionHealthBarColor { get; } =
        Binding(s => s.DamagePredictionHealthBarColor, (s, v) => s.DamagePredictionHealthBarColor = v);

    // Debug settings
    public static IModSettingsValueBinding<bool> DebugSettingsEnabled { get; } =
        Binding(s => s.DebugSettingsEnabled, (s, v) => s.DebugSettingsEnabled = v);

    public static IModSettingsValueBinding<bool> AncientEventDebugRerollEnabled { get; } =
        Binding(s => s.AncientEventDebugRerollEnabled, (s, v) => s.AncientEventDebugRerollEnabled = v);

    private static IModSettingsValueBinding<TValue> Binding<TValue>(
        Func<ModSettings, TValue> getter,
        Action<ModSettings, TValue> setter)
    {
        return ModSettingsBindings.WithDefault(
            ModSettingsBindings.Global(Entry.ModId, ModData.SettingsKey, getter, setter),
            () => getter(ModSettings.Default));
    }
}
