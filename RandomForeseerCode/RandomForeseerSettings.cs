using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib;
using STS2RitsuLib.Settings;
using STS2RitsuLib.Utils;
using STS2RitsuLib.Utils.Persistence;

namespace RandomForeseer.RandomForeseerCode;

internal sealed class RandomForeseerSettingsData
{
    public bool EnableSingleplayerPrediction { get; set; } = true;

    public bool EnableMultiplayerPrediction { get; set; } = true;

    public bool EnableFairMode { get; set; } = true;

    public bool EnableDriftWarnings { get; set; } = true;

    public bool EnableTransformPrediction { get; set; } = true;

    public bool EnableDriftwoodRerollPrediction { get; set; } = true;

    public bool EnablePaelsWingSacrificePrediction { get; set; } = true;

    public bool EnableRelicPickupPrediction { get; set; } = true;

    public bool EnableRestSitePrediction { get; set; } = true;

    public bool EnableEventOptionPrediction { get; set; } = true;

    public bool EnableCrystalSphereClairvoyance { get; set; } = true;

    public bool EnableNextActPrediction { get; set; } = true;

    public int SlipperyBridgeRerollPreviewCount { get; set; } = 5;

    public bool EnablePotionCardPrediction { get; set; } = true;

    public bool EnablePotionGenerationPrediction { get; set; } = true;

    public bool EnableCombatCardPrediction { get; set; } = true;

    public bool EnableCombatCardSelectionPrediction { get; set; } = true;

    public bool EnableOrbPrediction { get; set; } = true;

    public bool EnableEndTurnPrediction { get; set; } = true;

    public EndTurnPredictionDisplayMode EndTurnPredictionDisplayMode { get; set; } =
        EndTurnPredictionDisplayMode.EndTurnButtonHover;

    public EndTurnPredictionDisplayMode EndTurnHealthBarForecastDisplayMode { get; set; } =
        EndTurnPredictionDisplayMode.AlwaysDuringPlayerTurn;

    public string DamagePredictionHealthBarColor { get; set; } = "#E8C91A";

    public bool EnableAutoPlayFromDrawPilePrediction { get; set; } = true;

    public bool EnablePotionDrawPrediction { get; set; } = true;

    public bool EnableCardDrawPrediction { get; set; } = true;

    public bool EnableCombatTransformPrediction { get; set; } = true;

    public bool EnableFrozenEye { get; set; } = true;

    public bool EnableShufflePrediction { get; set; } = true;

    public bool ShowDebugSettingsPage { get; set; }

    public bool EnableAncientEventDebugReroll { get; set; }
}

internal static class RandomForeseerSettings
{
    private const string DataKey = "settings";

    private static bool _isDataRegistered;

    private static readonly RandomForeseerSettingsData Default = new();

    private static readonly I18N SettingsLocalization =
        RitsuLibFramework.CreateModLocalization(
            Entry.ModId,
            "settings",
            pckFolders: [$"{Entry.ResPath}/localization/settings"],
            resourceAssembly: Assembly.GetExecutingAssembly());

    private static readonly IModSettingsValueBinding<bool> EnableSingleplayerPredictionBinding =
        ModSettingsBindings.Global<RandomForeseerSettingsData, bool>(
            Entry.ModId,
            DataKey,
            settings => settings.EnableSingleplayerPrediction,
            (settings, value) => settings.EnableSingleplayerPrediction = value)
        .WithDefault(() => Default.EnableSingleplayerPrediction);

    private static readonly IModSettingsValueBinding<bool> EnableMultiplayerPredictionBinding =
        ModSettingsBindings.Global<RandomForeseerSettingsData, bool>(
            Entry.ModId,
            DataKey,
            settings => settings.EnableMultiplayerPrediction,
            (settings, value) => settings.EnableMultiplayerPrediction = value)
        .WithDefault(() => Default.EnableMultiplayerPrediction);

    private static readonly IModSettingsValueBinding<bool> EnableFairModeBinding =
        ModSettingsBindings.Global<RandomForeseerSettingsData, bool>(
            Entry.ModId,
            DataKey,
            settings => settings.EnableFairMode,
            (settings, value) => settings.EnableFairMode = value)
        .WithDefault(() => Default.EnableFairMode);

    private static readonly IModSettingsValueBinding<bool> EnableDriftWarningsBinding =
        ModSettingsBindings.Global<RandomForeseerSettingsData, bool>(
            Entry.ModId,
            DataKey,
            settings => settings.EnableDriftWarnings,
            (settings, value) => settings.EnableDriftWarnings = value)
        .WithDefault(() => Default.EnableDriftWarnings);

    private static readonly IModSettingsValueBinding<bool> EnableTransformPredictionBinding =
        ModSettingsBindings.Global<RandomForeseerSettingsData, bool>(
            Entry.ModId,
            DataKey,
            settings => settings.EnableTransformPrediction,
            (settings, value) => settings.EnableTransformPrediction = value)
        .WithDefault(() => Default.EnableTransformPrediction);

    private static readonly IModSettingsValueBinding<bool> EnableDriftwoodRerollPredictionBinding =
        ModSettingsBindings.Global<RandomForeseerSettingsData, bool>(
            Entry.ModId,
            DataKey,
            settings => settings.EnableDriftwoodRerollPrediction,
            (settings, value) => settings.EnableDriftwoodRerollPrediction = value)
        .WithDefault(() => Default.EnableDriftwoodRerollPrediction);

    private static readonly IModSettingsValueBinding<bool> EnablePaelsWingSacrificePredictionBinding =
        ModSettingsBindings.Global<RandomForeseerSettingsData, bool>(
            Entry.ModId,
            DataKey,
            settings => settings.EnablePaelsWingSacrificePrediction,
            (settings, value) => settings.EnablePaelsWingSacrificePrediction = value)
        .WithDefault(() => Default.EnablePaelsWingSacrificePrediction);

    private static readonly IModSettingsValueBinding<bool> EnableRelicPickupPredictionBinding =
        ModSettingsBindings.Global<RandomForeseerSettingsData, bool>(
            Entry.ModId,
            DataKey,
            settings => settings.EnableRelicPickupPrediction,
            (settings, value) => settings.EnableRelicPickupPrediction = value)
        .WithDefault(() => Default.EnableRelicPickupPrediction);

    private static readonly IModSettingsValueBinding<bool> EnableRestSitePredictionBinding =
        ModSettingsBindings.Global<RandomForeseerSettingsData, bool>(
            Entry.ModId,
            DataKey,
            settings => settings.EnableRestSitePrediction,
            (settings, value) => settings.EnableRestSitePrediction = value)
        .WithDefault(() => Default.EnableRestSitePrediction);

    private static readonly IModSettingsValueBinding<bool> EnableEventOptionPredictionBinding =
        ModSettingsBindings.Global<RandomForeseerSettingsData, bool>(
            Entry.ModId,
            DataKey,
            settings => settings.EnableEventOptionPrediction,
            (settings, value) => settings.EnableEventOptionPrediction = value)
        .WithDefault(() => Default.EnableEventOptionPrediction);

    private static readonly IModSettingsValueBinding<bool> EnableCrystalSphereClairvoyanceBinding =
        ModSettingsBindings.Global<RandomForeseerSettingsData, bool>(
            Entry.ModId,
            DataKey,
            settings => settings.EnableCrystalSphereClairvoyance,
            (settings, value) => settings.EnableCrystalSphereClairvoyance = value)
        .WithDefault(() => Default.EnableCrystalSphereClairvoyance);

    private static readonly IModSettingsValueBinding<bool> EnableNextActPredictionBinding =
        ModSettingsBindings.Global<RandomForeseerSettingsData, bool>(
            Entry.ModId,
            DataKey,
            settings => settings.EnableNextActPrediction,
            (settings, value) => settings.EnableNextActPrediction = value)
        .WithDefault(() => Default.EnableNextActPrediction);

    private static readonly IModSettingsValueBinding<int> SlipperyBridgeRerollPreviewCountBinding =
        ModSettingsBindings.Global<RandomForeseerSettingsData, int>(
            Entry.ModId,
            DataKey,
            settings => settings.SlipperyBridgeRerollPreviewCount,
            (settings, value) => settings.SlipperyBridgeRerollPreviewCount = value)
        .WithDefault(() => Default.SlipperyBridgeRerollPreviewCount);

    private static readonly IModSettingsValueBinding<bool> EnablePotionCardPredictionBinding =
        ModSettingsBindings.Global<RandomForeseerSettingsData, bool>(
            Entry.ModId,
            DataKey,
            settings => settings.EnablePotionCardPrediction,
            (settings, value) => settings.EnablePotionCardPrediction = value)
        .WithDefault(() => Default.EnablePotionCardPrediction);

    private static readonly IModSettingsValueBinding<bool> EnablePotionGenerationPredictionBinding =
        ModSettingsBindings.Global<RandomForeseerSettingsData, bool>(
            Entry.ModId,
            DataKey,
            settings => settings.EnablePotionGenerationPrediction,
            (settings, value) => settings.EnablePotionGenerationPrediction = value)
        .WithDefault(() => Default.EnablePotionGenerationPrediction);

    private static readonly IModSettingsValueBinding<bool> EnableCombatCardPredictionBinding =
        ModSettingsBindings.Global<RandomForeseerSettingsData, bool>(
            Entry.ModId,
            DataKey,
            settings => settings.EnableCombatCardPrediction,
            (settings, value) => settings.EnableCombatCardPrediction = value)
        .WithDefault(() => Default.EnableCombatCardPrediction);

    private static readonly IModSettingsValueBinding<bool> EnableCombatCardSelectionPredictionBinding =
        ModSettingsBindings.Global<RandomForeseerSettingsData, bool>(
            Entry.ModId,
            DataKey,
            settings => settings.EnableCombatCardSelectionPrediction,
            (settings, value) => settings.EnableCombatCardSelectionPrediction = value)
        .WithDefault(() => Default.EnableCombatCardSelectionPrediction);

    private static readonly IModSettingsValueBinding<bool> EnableOrbPredictionBinding =
        ModSettingsBindings.Global<RandomForeseerSettingsData, bool>(
            Entry.ModId,
            DataKey,
            settings => settings.EnableOrbPrediction,
            (settings, value) => settings.EnableOrbPrediction = value)
        .WithDefault(() => Default.EnableOrbPrediction);

    private static readonly IModSettingsValueBinding<bool> EnableEndTurnPredictionBinding =
        ModSettingsBindings.Global<RandomForeseerSettingsData, bool>(
            Entry.ModId,
            DataKey,
            settings => settings.EnableEndTurnPrediction,
            (settings, value) => settings.EnableEndTurnPrediction = value)
        .WithDefault(() => Default.EnableEndTurnPrediction);

    private static readonly IModSettingsValueBinding<EndTurnPredictionDisplayMode> EndTurnPredictionDisplayModeBinding =
        ModSettingsBindings.Global<RandomForeseerSettingsData, EndTurnPredictionDisplayMode>(
            Entry.ModId,
            DataKey,
            settings => settings.EndTurnPredictionDisplayMode,
            (settings, value) => settings.EndTurnPredictionDisplayMode = value)
        .WithDefault(() => Default.EndTurnPredictionDisplayMode);

    private static readonly IModSettingsValueBinding<EndTurnPredictionDisplayMode>
        EndTurnHealthBarForecastDisplayModeBinding =
            ModSettingsBindings.Global<RandomForeseerSettingsData, EndTurnPredictionDisplayMode>(
                Entry.ModId,
                DataKey,
                settings => settings.EndTurnHealthBarForecastDisplayMode,
                (settings, value) => settings.EndTurnHealthBarForecastDisplayMode = value)
            .WithDefault(() => Default.EndTurnHealthBarForecastDisplayMode);

    private static readonly IModSettingsValueBinding<string> DamagePredictionHealthBarColorBinding =
        ModSettingsBindings.Global<RandomForeseerSettingsData, string>(
            Entry.ModId,
            DataKey,
            settings => settings.DamagePredictionHealthBarColor,
            (settings, value) => settings.DamagePredictionHealthBarColor = value)
        .WithDefault(() => Default.DamagePredictionHealthBarColor);

    private static readonly IModSettingsValueBinding<bool> EnableAutoPlayFromDrawPilePredictionBinding =
        ModSettingsBindings.Global<RandomForeseerSettingsData, bool>(
            Entry.ModId,
            DataKey,
            settings => settings.EnableAutoPlayFromDrawPilePrediction,
            (settings, value) => settings.EnableAutoPlayFromDrawPilePrediction = value)
        .WithDefault(() => Default.EnableAutoPlayFromDrawPilePrediction);

    private static readonly IModSettingsValueBinding<bool> EnablePotionDrawPredictionBinding =
        ModSettingsBindings.Global<RandomForeseerSettingsData, bool>(
            Entry.ModId,
            DataKey,
            settings => settings.EnablePotionDrawPrediction,
            (settings, value) => settings.EnablePotionDrawPrediction = value)
        .WithDefault(() => Default.EnablePotionDrawPrediction);

    private static readonly IModSettingsValueBinding<bool> EnableCardDrawPredictionBinding =
        ModSettingsBindings.Global<RandomForeseerSettingsData, bool>(
            Entry.ModId,
            DataKey,
            settings => settings.EnableCardDrawPrediction,
            (settings, value) => settings.EnableCardDrawPrediction = value)
        .WithDefault(() => Default.EnableCardDrawPrediction);

    private static readonly IModSettingsValueBinding<bool> EnableCombatTransformPredictionBinding =
        ModSettingsBindings.Global<RandomForeseerSettingsData, bool>(
            Entry.ModId,
            DataKey,
            settings => settings.EnableCombatTransformPrediction,
            (settings, value) => settings.EnableCombatTransformPrediction = value)
        .WithDefault(() => Default.EnableCombatTransformPrediction);

    private static readonly IModSettingsValueBinding<bool> EnableFrozenEyeBinding =
        ModSettingsBindings.Global<RandomForeseerSettingsData, bool>(
            Entry.ModId,
            DataKey,
            settings => settings.EnableFrozenEye,
            (settings, value) => settings.EnableFrozenEye = value)
        .WithDefault(() => Default.EnableFrozenEye);

    private static readonly IModSettingsValueBinding<bool> EnableShufflePredictionBinding =
        ModSettingsBindings.Global<RandomForeseerSettingsData, bool>(
            Entry.ModId,
            DataKey,
            settings => settings.EnableShufflePrediction,
            (settings, value) => settings.EnableShufflePrediction = value)
        .WithDefault(() => Default.EnableShufflePrediction);

    private static readonly IModSettingsValueBinding<bool> ShowDebugSettingsPageBinding =
        ModSettingsBindings.Global<RandomForeseerSettingsData, bool>(
            Entry.ModId,
            DataKey,
            settings => settings.ShowDebugSettingsPage,
            (settings, value) => settings.ShowDebugSettingsPage = value)
        .WithDefault(() => Default.ShowDebugSettingsPage);

    private static readonly IModSettingsValueBinding<bool> EnableAncientEventDebugRerollBinding =
        ModSettingsBindings.Global<RandomForeseerSettingsData, bool>(
            Entry.ModId,
            DataKey,
            settings => settings.EnableAncientEventDebugReroll,
            (settings, value) => settings.EnableAncientEventDebugReroll = value)
        .WithDefault(() => Default.EnableAncientEventDebugReroll);

    public static bool EnableSingleplayerPrediction => EnableSingleplayerPredictionBinding.Read();

    public static bool EnableMultiplayerPrediction => EnableMultiplayerPredictionBinding.Read();

    public static bool EnableFairMode => EnableFairModeBinding.Read();

    public static bool EnableDriftWarnings => EnableDriftWarningsBinding.Read();

    public static bool EnableTransformPrediction => EnableTransformPredictionBinding.Read();

    public static bool EnableDriftwoodRerollPrediction => EnableDriftwoodRerollPredictionBinding.Read();

    public static bool EnablePaelsWingSacrificePrediction => EnablePaelsWingSacrificePredictionBinding.Read();

    public static bool EnableRelicPickupPrediction => EnableRelicPickupPredictionBinding.Read();

    public static bool EnableRestSitePrediction => EnableRestSitePredictionBinding.Read();

    public static bool EnableEventOptionPrediction => EnableEventOptionPredictionBinding.Read();

    public static bool EnableCrystalSphereClairvoyance => EnableCrystalSphereClairvoyanceBinding.Read();

    public static bool EnableNextActPrediction => EnableNextActPredictionBinding.Read();

    public static int SlipperyBridgeRerollPreviewCount => Math.Clamp(SlipperyBridgeRerollPreviewCountBinding.Read(), 1, 10);

    public static bool EnablePotionCardPrediction => EnablePotionCardPredictionBinding.Read();

    public static bool EnablePotionGenerationPrediction => EnablePotionGenerationPredictionBinding.Read();

    public static bool EnableCombatCardPrediction => EnableCombatCardPredictionBinding.Read();

    public static bool EnableCombatCardSelectionPrediction => EnableCombatCardSelectionPredictionBinding.Read();

    public static bool EnableOrbPrediction => EnableOrbPredictionBinding.Read();

    public static bool EnableEndTurnPrediction => EnableEndTurnPredictionBinding.Read();

    public static EndTurnPredictionDisplayMode EndTurnPredictionDisplayMode =>
        EndTurnPredictionDisplayModeBinding.Read();

    public static EndTurnPredictionDisplayMode EndTurnHealthBarForecastDisplayMode =>
        EndTurnHealthBarForecastDisplayModeBinding.Read();

    public static Color DamagePredictionHealthBarColor =>
        ModSettingsColorControl.TryDeserializeColorForSettings(
            DamagePredictionHealthBarColorBinding.Read(),
            out var color)
            ? color
            : new("E8C91A");

    public static bool EnableAutoPlayFromDrawPilePrediction => EnableAutoPlayFromDrawPilePredictionBinding.Read();

    public static bool EnablePotionDrawPrediction => EnablePotionDrawPredictionBinding.Read();

    public static bool EnableCardDrawPrediction => EnableCardDrawPredictionBinding.Read();

    public static bool EnableCombatTransformPrediction => EnableCombatTransformPredictionBinding.Read();

    public static bool EnableFrozenEye => EnableFrozenEyeBinding.Read();

    public static bool EnableShufflePrediction => EnableShufflePredictionBinding.Read();

    public static bool ShowDebugSettingsPage => ShowDebugSettingsPageBinding.Read();

    public static bool EnableAncientEventDebugReroll => EnableAncientEventDebugRerollBinding.Read();

    public static bool IsEndTurnPredictionRefreshBinding(IModSettingsBinding binding)
    {
        return ReferenceEquals(binding, EnableEndTurnPredictionBinding) ||
            ReferenceEquals(binding, EndTurnPredictionDisplayModeBinding) ||
            ReferenceEquals(binding, EndTurnHealthBarForecastDisplayModeBinding);
    }

    public static bool IsDamagePredictionHealthBarColorBinding(IModSettingsBinding binding)
    {
        return ReferenceEquals(binding, DamagePredictionHealthBarColorBinding);
    }

    public static bool IsPredictionFeatureEnabled(bool featureEnabled)
    {
        if (!featureEnabled)
        {
            return false;
        }

        return GetCurrentNetGameType() switch
        {
            NetGameType.Singleplayer => EnableSingleplayerPrediction,
            NetGameType.Host or NetGameType.Client => EnableMultiplayerPrediction,
            _ => true
        };
    }

    public static bool IsFairPredictionAllowed(PredictionFairness fairness)
    {
        if (!EnableFairMode)
        {
            return true;
        }

        return fairness switch
        {
            PredictionFairness.Fair => true,
            PredictionFairness.UnfairInSingleplayer => GetCurrentNetGameType() != NetGameType.Singleplayer,
            PredictionFairness.UnfairInAllModes => false,
            _ => true
        };
    }

    public static void Register()
    {
        RegisterData();

        RitsuLibFramework.RegisterModSettings(Entry.ModId, page =>
        {
            page.WithModDisplayName(Text("mod.name"));
            page.WithTitle(Text("page.title"));
            page.WithSortOrder(0);
            page.WithDescription(Text("page.description"));

            page.AddSection("general_prediction", section =>
            {
                section.WithTitle(Text("section.general_prediction.title"));
                section.WithDescription(Text("section.general_prediction.description"));

                section.AddToggle(
                    "enable_singleplayer_prediction",
                    Text("toggle.enable_singleplayer_prediction.label"),
                    EnableSingleplayerPredictionBinding,
                    Text("toggle.enable_singleplayer_prediction.description"));

                section.AddToggle(
                    "enable_multiplayer_prediction",
                    Text("toggle.enable_multiplayer_prediction.label"),
                    EnableMultiplayerPredictionBinding,
                    Text("toggle.enable_multiplayer_prediction.description"));

                section.AddToggle(
                    "enable_fair_mode",
                    Text("toggle.enable_fair_mode.label"),
                    EnableFairModeBinding,
                    Text("toggle.enable_fair_mode.description"));

                section.AddToggle(
                    "enable_drift_warnings",
                    Text("toggle.enable_drift_warnings.label"),
                    EnableDriftWarningsBinding,
                    Text("toggle.enable_drift_warnings.description"));
            });

            page.AddSection("out_of_combat_prediction", section =>
            {
                section.WithTitle(Text("section.out_of_combat_prediction.title"));
                section.WithDescription(Text("section.out_of_combat_prediction.description"));

                section.AddToggle(
                    "enable_transform_prediction",
                    Text("toggle.enable_transform_prediction.label"),
                    EnableTransformPredictionBinding,
                    Text("toggle.enable_transform_prediction.description"));

                section.AddToggle(
                    "enable_driftwood_reroll_prediction",
                    Text("toggle.enable_driftwood_reroll_prediction.label"),
                    EnableDriftwoodRerollPredictionBinding,
                    Text("toggle.enable_driftwood_reroll_prediction.description"));

                section.AddToggle(
                    "enable_paels_wing_sacrifice_prediction",
                    Text("toggle.enable_paels_wing_sacrifice_prediction.label"),
                    EnablePaelsWingSacrificePredictionBinding,
                    Text("toggle.enable_paels_wing_sacrifice_prediction.description"));

                section.AddToggle(
                    "enable_relic_pickup_prediction",
                    Text("toggle.enable_relic_pickup_prediction.label"),
                    EnableRelicPickupPredictionBinding,
                    Text("toggle.enable_relic_pickup_prediction.description"));

                section.AddToggle(
                    "enable_rest_site_prediction",
                    Text("toggle.enable_rest_site_prediction.label"),
                    EnableRestSitePredictionBinding,
                    Text("toggle.enable_rest_site_prediction.description"));

                section.AddToggle(
                    "enable_event_option_prediction",
                    Text("toggle.enable_event_option_prediction.label"),
                    EnableEventOptionPredictionBinding,
                    Text("toggle.enable_event_option_prediction.description"));

                section.AddToggle(
                    "enable_crystal_sphere_clairvoyance",
                    Text("toggle.enable_crystal_sphere_clairvoyance.label"),
                    EnableCrystalSphereClairvoyanceBinding,
                    Text("toggle.enable_crystal_sphere_clairvoyance.description"));

                section.AddIntSlider(
                    "slippery_bridge_reroll_preview_count",
                    Text("slider.slippery_bridge_reroll_preview_count.label"),
                    SlipperyBridgeRerollPreviewCountBinding,
                    1,
                    10,
                    1,
                    value => value.ToString(),
                    Text("slider.slippery_bridge_reroll_preview_count.description"));

                section.AddToggle(
                    "enable_next_act_prediction",
                    Text("toggle.enable_next_act_prediction.label"),
                    EnableNextActPredictionBinding,
                    Text("toggle.enable_next_act_prediction.description"));
            });

            page.AddSection("in_combat_prediction", section =>
            {
                section.WithTitle(Text("section.in_combat_prediction.title"));
                section.WithDescription(Text("section.in_combat_prediction.description"));

                section.AddToggle(
                    "enable_potion_card_prediction",
                    Text("toggle.enable_potion_card_prediction.label"),
                    EnablePotionCardPredictionBinding,
                    Text("toggle.enable_potion_card_prediction.description"));

                section.AddToggle(
                    "enable_potion_generation_prediction",
                    Text("toggle.enable_potion_generation_prediction.label"),
                    EnablePotionGenerationPredictionBinding,
                    Text("toggle.enable_potion_generation_prediction.description"));

                section.AddToggle(
                    "enable_combat_card_prediction",
                    Text("toggle.enable_combat_card_prediction.label"),
                    EnableCombatCardPredictionBinding,
                    Text("toggle.enable_combat_card_prediction.description"));

                section.AddToggle(
                    "enable_combat_card_selection_prediction",
                    Text("toggle.enable_combat_card_selection_prediction.label"),
                    EnableCombatCardSelectionPredictionBinding,
                    Text("toggle.enable_combat_card_selection_prediction.description"));

                section.AddToggle(
                    "enable_orb_prediction",
                    Text("toggle.enable_orb_prediction.label"),
                    EnableOrbPredictionBinding,
                    Text("toggle.enable_orb_prediction.description"));

                section.AddToggle(
                    "enable_end_turn_prediction",
                    Text("toggle.enable_end_turn_prediction.label"),
                    EnableEndTurnPredictionBinding,
                    Text("toggle.enable_end_turn_prediction.description"));

                section.AddEnumChoice(
                    "end_turn_prediction_display_mode",
                    Text("choice.end_turn_prediction_display_mode.label"),
                    EndTurnPredictionDisplayModeBinding,
                    value => Text($"choice.end_turn_prediction_display_mode.option.{value}"),
                    Text("choice.end_turn_prediction_display_mode.description"),
                    ModSettingsChoicePresentation.Dropdown);

                section.AddEnumChoice(
                    "end_turn_health_bar_forecast_display_mode",
                    Text("choice.end_turn_health_bar_forecast_display_mode.label"),
                    EndTurnHealthBarForecastDisplayModeBinding,
                    value => Text($"choice.end_turn_health_bar_forecast_display_mode.option.{value}"),
                    Text("choice.end_turn_health_bar_forecast_display_mode.description"),
                    ModSettingsChoicePresentation.Dropdown);

                section.AddColor(
                    "damage_prediction_health_bar_color",
                    Text("color.damage_prediction_health_bar_color.label"),
                    DamagePredictionHealthBarColorBinding,
                    Text("color.damage_prediction_health_bar_color.description"),
                    editAlpha: true,
                    editIntensity: false);

                section.AddToggle(
                    "enable_auto_play_from_draw_pile_prediction",
                    Text("toggle.enable_auto_play_from_draw_pile_prediction.label"),
                    EnableAutoPlayFromDrawPilePredictionBinding,
                    Text("toggle.enable_auto_play_from_draw_pile_prediction.description"));

                section.AddToggle(
                    "enable_potion_draw_prediction",
                    Text("toggle.enable_potion_draw_prediction.label"),
                    EnablePotionDrawPredictionBinding,
                    Text("toggle.enable_potion_draw_prediction.description"));

                section.AddToggle(
                    "enable_card_draw_prediction",
                    Text("toggle.enable_card_draw_prediction.label"),
                    EnableCardDrawPredictionBinding,
                    Text("toggle.enable_card_draw_prediction.description"));

                section.AddToggle(
                    "enable_combat_transform_prediction",
                    Text("toggle.enable_combat_transform_prediction.label"),
                    EnableCombatTransformPredictionBinding,
                    Text("toggle.enable_combat_transform_prediction.description"));

                section.AddToggle(
                    "enable_frozen_eye",
                    Text("toggle.enable_frozen_eye.label"),
                    EnableFrozenEyeBinding,
                    Text("toggle.enable_frozen_eye.description"));

                section.AddToggle(
                    "enable_shuffle_prediction",
                    Text("toggle.enable_shuffle_prediction.label"),
                    EnableShufflePredictionBinding,
                    Text("toggle.enable_shuffle_prediction.description"));
            });

        });

        if (!ShowDebugSettingsPage)
        {
            return;
        }

        RitsuLibFramework.RegisterModSettings(Entry.ModId, page =>
        {
            page.WithModDisplayName(Text("mod.name"));
            page.WithTitle(Text("page.debug.title"));
            page.WithSortOrder(1);
            page.WithDescription(Text("page.debug.description"));

            page.AddSection("ancient_event_debug", section =>
            {
                section.WithTitle(Text("section.ancient_event_debug.title"));
                section.WithDescription(Text("section.ancient_event_debug.description"));

                section.AddToggle(
                    "enable_ancient_event_debug_reroll",
                    Text("toggle.enable_ancient_event_debug_reroll.label"),
                    EnableAncientEventDebugRerollBinding,
                    Text("toggle.enable_ancient_event_debug_reroll.description"));
            });

            page.AddSection("relic_pickup_debug", section =>
            {
                section.WithTitle(Text("section.relic_pickup_debug.title"));
                section.WithDescription(Text("section.relic_pickup_debug.description"));

                section.AddButton(
                    "offer_predicted_non_ancient_relics",
                    Text("button.offer_predicted_non_ancient_relics.label"),
                    Text("button.offer_predicted_non_ancient_relics.text"),
                    Debug.RelicPickupDebugRewards.OfferPredictedNonAncientRelics,
                    ModSettingsButtonTone.Danger,
                    Text("button.offer_predicted_non_ancient_relics.description"));

                section.AddButton(
                    "open_predicted_treasure_room",
                    Text("button.open_predicted_treasure_room.label"),
                    Text("button.open_predicted_treasure_room.text"),
                    Debug.RelicPickupDebugRewards.OpenPredictedTreasureRoom,
                    ModSettingsButtonTone.Danger,
                    Text("button.open_predicted_treasure_room.description"));

                section.AddButton(
                    "open_relic_trader_pickup_test",
                    Text("button.open_relic_trader_pickup_test.label"),
                    Text("button.open_relic_trader_pickup_test.text"),
                    Debug.RelicPickupDebugRewards.OpenRelicTraderPickupTest,
                    ModSettingsButtonTone.Danger,
                    Text("button.open_relic_trader_pickup_test.description"));
            });
        }, "debug");
    }

    private static void RegisterData()
    {
        if (_isDataRegistered)
        {
            return;
        }

        using (RitsuLibFramework.BeginModDataRegistration(Entry.ModId))
        {
            RitsuLibFramework.GetDataStore(Entry.ModId).Register(
                DataKey,
                "settings.json",
                SaveScope.Global,
                () => new RandomForeseerSettingsData(),
                autoCreateIfMissing: true,
                migrationConfig: null,
                migrations: null);
        }

        _isDataRegistered = true;
    }

    private static ModSettingsText Text(string key)
    {
        return ModSettingsText.I18N(SettingsLocalization, key, key);
    }

    private static NetGameType GetCurrentNetGameType()
    {
        return RunManager.Instance.IsInProgress
            ? RunManager.Instance.NetService.Type
            : NetGameType.None;
    }
}

internal static class ModSettingsBindingExtensions
{
    public static DefaultModSettingsValueBinding<TValue> WithDefault<TValue>(
        this IModSettingsValueBinding<TValue> binding,
        Func<TValue> defaultValueFactory)
    {
        return ModSettingsBindings.WithDefault(binding, defaultValueFactory);
    }
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
