using System.Reflection;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib;
using STS2RitsuLib.Settings;
using STS2RitsuLib.Utils;
using STS2RitsuLib.Utils.Persistence;

namespace RandomForeseer;

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
            (settings, value) => settings.EnableSingleplayerPrediction = value);

    private static readonly IModSettingsValueBinding<bool> EnableMultiplayerPredictionBinding =
        ModSettingsBindings.Global<RandomForeseerSettingsData, bool>(
            Entry.ModId,
            DataKey,
            settings => settings.EnableMultiplayerPrediction,
            (settings, value) => settings.EnableMultiplayerPrediction = value);

    private static readonly IModSettingsValueBinding<bool> EnableFairModeBinding =
        ModSettingsBindings.Global<RandomForeseerSettingsData, bool>(
            Entry.ModId,
            DataKey,
            settings => settings.EnableFairMode,
            (settings, value) => settings.EnableFairMode = value);

    private static readonly IModSettingsValueBinding<bool> EnableDriftWarningsBinding =
        ModSettingsBindings.Global<RandomForeseerSettingsData, bool>(
            Entry.ModId,
            DataKey,
            settings => settings.EnableDriftWarnings,
            (settings, value) => settings.EnableDriftWarnings = value);

    private static readonly IModSettingsValueBinding<bool> EnableTransformPredictionBinding =
        ModSettingsBindings.Global<RandomForeseerSettingsData, bool>(
            Entry.ModId,
            DataKey,
            settings => settings.EnableTransformPrediction,
            (settings, value) => settings.EnableTransformPrediction = value);

    private static readonly IModSettingsValueBinding<bool> EnableDriftwoodRerollPredictionBinding =
        ModSettingsBindings.Global<RandomForeseerSettingsData, bool>(
            Entry.ModId,
            DataKey,
            settings => settings.EnableDriftwoodRerollPrediction,
            (settings, value) => settings.EnableDriftwoodRerollPrediction = value);

    private static readonly IModSettingsValueBinding<bool> EnablePaelsWingSacrificePredictionBinding =
        ModSettingsBindings.Global<RandomForeseerSettingsData, bool>(
            Entry.ModId,
            DataKey,
            settings => settings.EnablePaelsWingSacrificePrediction,
            (settings, value) => settings.EnablePaelsWingSacrificePrediction = value);

    private static readonly IModSettingsValueBinding<bool> EnableRelicPickupPredictionBinding =
        ModSettingsBindings.Global<RandomForeseerSettingsData, bool>(
            Entry.ModId,
            DataKey,
            settings => settings.EnableRelicPickupPrediction,
            (settings, value) => settings.EnableRelicPickupPrediction = value);

    private static readonly IModSettingsValueBinding<bool> EnableRestSitePredictionBinding =
        ModSettingsBindings.Global<RandomForeseerSettingsData, bool>(
            Entry.ModId,
            DataKey,
            settings => settings.EnableRestSitePrediction,
            (settings, value) => settings.EnableRestSitePrediction = value);

    private static readonly IModSettingsValueBinding<bool> EnableEventOptionPredictionBinding =
        ModSettingsBindings.Global<RandomForeseerSettingsData, bool>(
            Entry.ModId,
            DataKey,
            settings => settings.EnableEventOptionPrediction,
            (settings, value) => settings.EnableEventOptionPrediction = value);

    private static readonly IModSettingsValueBinding<bool> EnableCrystalSphereClairvoyanceBinding =
        ModSettingsBindings.Global<RandomForeseerSettingsData, bool>(
            Entry.ModId,
            DataKey,
            settings => settings.EnableCrystalSphereClairvoyance,
            (settings, value) => settings.EnableCrystalSphereClairvoyance = value);

    private static readonly IModSettingsValueBinding<int> SlipperyBridgeRerollPreviewCountBinding =
        ModSettingsBindings.Global<RandomForeseerSettingsData, int>(
            Entry.ModId,
            DataKey,
            settings => settings.SlipperyBridgeRerollPreviewCount,
            (settings, value) => settings.SlipperyBridgeRerollPreviewCount = value);

    private static readonly IModSettingsValueBinding<bool> EnablePotionCardPredictionBinding =
        ModSettingsBindings.Global<RandomForeseerSettingsData, bool>(
            Entry.ModId,
            DataKey,
            settings => settings.EnablePotionCardPrediction,
            (settings, value) => settings.EnablePotionCardPrediction = value);

    private static readonly IModSettingsValueBinding<bool> EnablePotionGenerationPredictionBinding =
        ModSettingsBindings.Global<RandomForeseerSettingsData, bool>(
            Entry.ModId,
            DataKey,
            settings => settings.EnablePotionGenerationPrediction,
            (settings, value) => settings.EnablePotionGenerationPrediction = value);

    private static readonly IModSettingsValueBinding<bool> EnableCombatCardPredictionBinding =
        ModSettingsBindings.Global<RandomForeseerSettingsData, bool>(
            Entry.ModId,
            DataKey,
            settings => settings.EnableCombatCardPrediction,
            (settings, value) => settings.EnableCombatCardPrediction = value);

    private static readonly IModSettingsValueBinding<bool> EnableCombatCardSelectionPredictionBinding =
        ModSettingsBindings.Global<RandomForeseerSettingsData, bool>(
            Entry.ModId,
            DataKey,
            settings => settings.EnableCombatCardSelectionPrediction,
            (settings, value) => settings.EnableCombatCardSelectionPrediction = value);

    private static readonly IModSettingsValueBinding<bool> EnableOrbPredictionBinding =
        ModSettingsBindings.Global<RandomForeseerSettingsData, bool>(
            Entry.ModId,
            DataKey,
            settings => settings.EnableOrbPrediction,
            (settings, value) => settings.EnableOrbPrediction = value);

    private static readonly IModSettingsValueBinding<bool> EnableEndTurnPredictionBinding =
        ModSettingsBindings.Global<RandomForeseerSettingsData, bool>(
            Entry.ModId,
            DataKey,
            settings => settings.EnableEndTurnPrediction,
            (settings, value) => settings.EnableEndTurnPrediction = value);

    private static readonly IModSettingsValueBinding<EndTurnPredictionDisplayMode> EndTurnPredictionDisplayModeBinding =
        ModSettingsBindings.Global<RandomForeseerSettingsData, EndTurnPredictionDisplayMode>(
            Entry.ModId,
            DataKey,
            settings => settings.EndTurnPredictionDisplayMode,
            (settings, value) => settings.EndTurnPredictionDisplayMode = value);

    private static readonly IModSettingsValueBinding<EndTurnPredictionDisplayMode>
        EndTurnHealthBarForecastDisplayModeBinding =
            ModSettingsBindings.Global<RandomForeseerSettingsData, EndTurnPredictionDisplayMode>(
                Entry.ModId,
                DataKey,
                settings => settings.EndTurnHealthBarForecastDisplayMode,
                (settings, value) => settings.EndTurnHealthBarForecastDisplayMode = value);

    private static readonly IModSettingsValueBinding<bool> EnableAutoPlayFromDrawPilePredictionBinding =
        ModSettingsBindings.Global<RandomForeseerSettingsData, bool>(
            Entry.ModId,
            DataKey,
            settings => settings.EnableAutoPlayFromDrawPilePrediction,
            (settings, value) => settings.EnableAutoPlayFromDrawPilePrediction = value);

    private static readonly IModSettingsValueBinding<bool> EnablePotionDrawPredictionBinding =
        ModSettingsBindings.Global<RandomForeseerSettingsData, bool>(
            Entry.ModId,
            DataKey,
            settings => settings.EnablePotionDrawPrediction,
            (settings, value) => settings.EnablePotionDrawPrediction = value);

    private static readonly IModSettingsValueBinding<bool> EnableCardDrawPredictionBinding =
        ModSettingsBindings.Global<RandomForeseerSettingsData, bool>(
            Entry.ModId,
            DataKey,
            settings => settings.EnableCardDrawPrediction,
            (settings, value) => settings.EnableCardDrawPrediction = value);

    private static readonly IModSettingsValueBinding<bool> EnableCombatTransformPredictionBinding =
        ModSettingsBindings.Global<RandomForeseerSettingsData, bool>(
            Entry.ModId,
            DataKey,
            settings => settings.EnableCombatTransformPrediction,
            (settings, value) => settings.EnableCombatTransformPrediction = value);

    private static readonly IModSettingsValueBinding<bool> EnableFrozenEyeBinding =
        ModSettingsBindings.Global<RandomForeseerSettingsData, bool>(
            Entry.ModId,
            DataKey,
            settings => settings.EnableFrozenEye,
            (settings, value) => settings.EnableFrozenEye = value);

    private static readonly IModSettingsValueBinding<bool> EnableShufflePredictionBinding =
        ModSettingsBindings.Global<RandomForeseerSettingsData, bool>(
            Entry.ModId,
            DataKey,
            settings => settings.EnableShufflePrediction,
            (settings, value) => settings.EnableShufflePrediction = value);

    private static readonly IModSettingsValueBinding<bool> ShowDebugSettingsPageBinding =
        ModSettingsBindings.Global<RandomForeseerSettingsData, bool>(
            Entry.ModId,
            DataKey,
            settings => settings.ShowDebugSettingsPage,
            (settings, value) => settings.ShowDebugSettingsPage = value);

    private static readonly IModSettingsValueBinding<bool> EnableAncientEventDebugRerollBinding =
        ModSettingsBindings.Global<RandomForeseerSettingsData, bool>(
            Entry.ModId,
            DataKey,
            settings => settings.EnableAncientEventDebugReroll,
            (settings, value) => settings.EnableAncientEventDebugReroll = value);

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
            page.WithModDisplayName(Text("mod.name", "Random Foreseer"));
            page.WithTitle(Text("page.title", "Random Foreseer"));
            page.WithSortOrder(0);
            page.WithDescription(Text(
                "page.description",
                "Configure prediction features for random outcomes. Some settings may require Save & Load to take effect."));

            page.AddSection("general_prediction", section =>
            {
                section.WithTitle(Text("section.general_prediction.title", "General settings"));
                section.WithDescription(Text(
                    "section.general_prediction.description",
                    "Controls prediction availability across game modes."));

                section.AddToggle(
                    "enable_singleplayer_prediction",
                    Text("toggle.enable_singleplayer_prediction.label", "Enable singleplayer prediction"),
                    EnableSingleplayerPredictionBinding,
                    Text(
                        "toggle.enable_singleplayer_prediction.description",
                        "When disabled, prediction features do not take effect in singleplayer even if their individual settings are enabled."));

                section.AddToggle(
                    "enable_multiplayer_prediction",
                    Text("toggle.enable_multiplayer_prediction.label", "Enable multiplayer prediction"),
                    EnableMultiplayerPredictionBinding,
                    Text(
                        "toggle.enable_multiplayer_prediction.description",
                        "When disabled, prediction features do not take effect in multiplayer even if their individual settings are enabled."));

                section.AddToggle(
                    "enable_fair_mode",
                    Text("toggle.enable_fair_mode.label", "Enable fair mode"),
                    EnableFairModeBinding,
                    Text(
                        "toggle.enable_fair_mode.description",
                        "When enabled, prediction is limited to information that can be obtained through Save & Load."));

                section.AddToggle(
                    "enable_drift_warnings",
                    Text("toggle.enable_drift_warnings.label", "Show prediction drift warnings"),
                    EnableDriftWarningsBinding,
                    Text(
                        "toggle.enable_drift_warnings.description",
                        "When enabled, predictions that may be shifted by side effects show a warning tooltip."));
            });

            page.AddSection("out_of_combat_prediction", section =>
            {
                section.WithTitle(Text("section.out_of_combat_prediction.title", "Out-of-combat prediction"));
                section.WithDescription(Text(
                    "section.out_of_combat_prediction.description",
                    "Controls random outcomes shown outside combat."));

                section.AddToggle(
                    "enable_transform_prediction",
                    Text("toggle.enable_transform_prediction.label", "Predict transform results"),
                    EnableTransformPredictionBinding,
                    Text(
                        "toggle.enable_transform_prediction.description",
                        "When enabled, deck transform confirmation previews show the exact card the current RNG will produce."));

                section.AddToggle(
                    "enable_driftwood_reroll_prediction",
                    Text("toggle.enable_driftwood_reroll_prediction.label", "Predict Driftwood rerolls"),
                    EnableDriftwoodRerollPredictionBinding,
                    Text(
                        "toggle.enable_driftwood_reroll_prediction.description",
                        "When enabled, Driftwood's card reward reroll button shows the exact cards the reroll will offer."));

                section.AddToggle(
                    "enable_paels_wing_sacrifice_prediction",
                    Text("toggle.enable_paels_wing_sacrifice_prediction.label", "Predict Pael's Wing sacrifices"),
                    EnablePaelsWingSacrificePredictionBinding,
                    Text(
                        "toggle.enable_paels_wing_sacrifice_prediction.description",
                        "When enabled, Pael's Wing's Sacrifice button shows the relic awarded by the next activating sacrifice."));

                section.AddToggle(
                    "enable_relic_pickup_prediction",
                    Text("toggle.enable_relic_pickup_prediction.label", "Predict relic pickup effects"),
                    EnableRelicPickupPredictionBinding,
                    Text(
                        "toggle.enable_relic_pickup_prediction.description",
                        "When enabled, relic tooltips (including Ancient options) show random cards, relics, potions, curses, and transform results that happen immediately on pickup."));

                section.AddToggle(
                    "enable_rest_site_prediction",
                    Text("toggle.enable_rest_site_prediction.label", "Predict rest-site results"),
                    EnableRestSitePredictionBinding,
                    Text(
                        "toggle.enable_rest_site_prediction.description",
                        "When enabled, rest-site option tooltips show immediate random results from relics such as Dream Catcher, Tiny Mailbox, and Shovel."));

                section.AddToggle(
                    "enable_event_option_prediction",
                    Text("toggle.enable_event_option_prediction.label", "Predict event option results"),
                    EnableEventOptionPredictionBinding,
                    Text(
                        "toggle.enable_event_option_prediction.description",
                        "When enabled, non-Ancient event option tooltips show immediate random results such as rewards, upgrades, and offered follow-up choices."));

                section.AddToggle(
                    "enable_crystal_sphere_clairvoyance",
                    Text("toggle.enable_crystal_sphere_clairvoyance.label", "Enable Crystal Sphere clairvoyance"),
                    EnableCrystalSphereClairvoyanceBinding,
                    Text(
                        "toggle.enable_crystal_sphere_clairvoyance.description",
                        "When enabled, the Crystal Sphere minigame shows items through unrevealed fog."));

                section.AddIntSlider(
                    "slippery_bridge_reroll_preview_count",
                    Text("slider.slippery_bridge_reroll_preview_count.label", "Slippery Bridge reroll previews"),
                    SlipperyBridgeRerollPreviewCountBinding,
                    1,
                    10,
                    1,
                    value => value.ToString(),
                    Text(
                        "slider.slippery_bridge_reroll_preview_count.description",
                        "How many future Hold On rerolls to preview for Slippery Bridge."));
            });

            page.AddSection("in_combat_prediction", section =>
            {
                section.WithTitle(Text("section.in_combat_prediction.title", "In-combat prediction"));
                section.WithDescription(Text(
                    "section.in_combat_prediction.description",
                    "Controls random outcomes shown during combat."));

                section.AddToggle(
                    "enable_potion_card_prediction",
                    Text("toggle.enable_potion_card_prediction.label", "Predict potion card results"),
                    EnablePotionCardPredictionBinding,
                    Text(
                        "toggle.enable_potion_card_prediction.description",
                        "When enabled, random-card potion tooltips show the exact cards the current RNG will produce."));

                section.AddToggle(
                    "enable_potion_generation_prediction",
                    Text("toggle.enable_potion_generation_prediction.label", "Predict potion generation"),
                    EnablePotionGenerationPredictionBinding,
                    Text(
                        "toggle.enable_potion_generation_prediction.description",
                        "When enabled, Entropic Brew and Alchemize tooltips show the exact potions the current RNG will produce."));

                section.AddToggle(
                    "enable_combat_card_prediction",
                    Text("toggle.enable_combat_card_prediction.label", "Predict combat card generation"),
                    EnableCombatCardPredictionBinding,
                    Text(
                        "toggle.enable_combat_card_prediction.description",
                        "When enabled, combat card tooltips show the exact random cards the current RNG will generate."));

                section.AddToggle(
                    "enable_combat_card_selection_prediction",
                    Text("toggle.enable_combat_card_selection_prediction.label", "Predict combat card selection"),
                    EnableCombatCardSelectionPredictionBinding,
                    Text(
                        "toggle.enable_combat_card_selection_prediction.description",
                        "When enabled, combat card tooltips and hand highlights show the exact existing cards the current RNG will select."));

                section.AddToggle(
                    "enable_orb_prediction",
                    Text("toggle.enable_orb_prediction.label", "Predict orb effects"),
                    EnableOrbPredictionBinding,
                    Text(
                        "toggle.enable_orb_prediction.description",
                        "When enabled, supported orb-triggering card tooltips show the targets that orb effects will hit."));

                section.AddToggle(
                    "enable_end_turn_prediction",
                    Text("toggle.enable_end_turn_prediction.label", "Predict end-turn effects"),
                    EnableEndTurnPredictionBinding,
                    Text(
                        "toggle.enable_end_turn_prediction.description",
                        "When enabled, supported end-turn damage effects are previewed during combat."));

                section.AddEnumChoice(
                    "end_turn_prediction_display_mode",
                    Text("choice.end_turn_prediction_display_mode.label", "End-turn prediction overlay display"),
                    EndTurnPredictionDisplayModeBinding,
                    value => Text(
                        $"choice.end_turn_prediction_display_mode.option.{value}",
                        value switch
                        {
                            EndTurnPredictionDisplayMode.AlwaysDuringPlayerTurn => "Always during player turn",
                            _ => "End Turn button hover"
                        }),
                    Text(
                        "choice.end_turn_prediction_display_mode.description",
                        "Controls when end-turn damage prediction overlay indicators are shown."),
                    ModSettingsChoicePresentation.Dropdown);

                section.AddEnumChoice(
                    "end_turn_health_bar_forecast_display_mode",
                    Text(
                        "choice.end_turn_health_bar_forecast_display_mode.label",
                        "End-turn health bar forecast display"),
                    EndTurnHealthBarForecastDisplayModeBinding,
                    value => Text(
                        $"choice.end_turn_health_bar_forecast_display_mode.option.{value}",
                        value switch
                        {
                            EndTurnPredictionDisplayMode.AlwaysDuringPlayerTurn => "Always during player turn",
                            _ => "End Turn button hover"
                        }),
                    Text(
                        "choice.end_turn_health_bar_forecast_display_mode.description",
                        "Controls when end-turn damage prediction is shown on target health bars."),
                    ModSettingsChoicePresentation.Dropdown);

                section.AddToggle(
                    "enable_auto_play_from_draw_pile_prediction",
                    Text("toggle.enable_auto_play_from_draw_pile_prediction.label", "Predict draw-pile autoplay"),
                    EnableAutoPlayFromDrawPilePredictionBinding,
                    Text(
                        "toggle.enable_auto_play_from_draw_pile_prediction.description",
                        "When enabled, Havoc, Cascade, and Distilled Chaos tooltips show the cards that will be played from the draw pile."));

                section.AddToggle(
                    "enable_potion_draw_prediction",
                    Text("toggle.enable_potion_draw_prediction.label", "Predict potion draw"),
                    EnablePotionDrawPredictionBinding,
                    Text(
                        "toggle.enable_potion_draw_prediction.description",
                        "When enabled, supported draw potions show the cards that will be drawn, including cards after shuffle."));

                section.AddToggle(
                    "enable_card_draw_prediction",
                    Text("toggle.enable_card_draw_prediction.label", "Predict card draw"),
                    EnableCardDrawPredictionBinding,
                    Text(
                        "toggle.enable_card_draw_prediction.description",
                        "When enabled, Reboot and Calculated Gamble show the cards that will be drawn, including cards after shuffle."));

                section.AddToggle(
                    "enable_combat_transform_prediction",
                    Text("toggle.enable_combat_transform_prediction.label", "Predict combat transform results"),
                    EnableCombatTransformPredictionBinding,
                    Text(
                        "toggle.enable_combat_transform_prediction.description",
                        "When enabled, combat transform selections show the exact card the current RNG will produce."));

                section.AddToggle(
                    "enable_frozen_eye",
                    Text("toggle.enable_frozen_eye.label", "Enable Frozen Eye"),
                    EnableFrozenEyeBinding,
                    Text(
                        "toggle.enable_frozen_eye.description",
                        "When enabled, the combat draw pile view shows cards in draw order."));

                section.AddToggle(
                    "enable_shuffle_prediction",
                    Text("toggle.enable_shuffle_prediction.label", "Predict shuffle order"),
                    EnableShufflePredictionBinding,
                    Text(
                        "toggle.enable_shuffle_prediction.description",
                        "When enabled, the Frozen Eye draw pile view previews the order in which the discard pile will be shuffled into the draw pile."));
            });

        });

        if (!ShowDebugSettingsPage)
        {
            return;
        }

        RitsuLibFramework.RegisterModSettings(Entry.ModId, page =>
        {
            page.WithModDisplayName(Text("mod.name", "Random Foreseer"));
            page.WithTitle(Text("page.debug.title", "Debug"));
            page.WithSortOrder(1);
            page.WithDescription(Text(
                "page.debug.description",
                "Controls tools intended for development and verification."));

            page.AddSection("ancient_event_debug", section =>
            {
                section.WithTitle(Text("section.ancient_event_debug.title", "Ancient events"));
                section.WithDescription(Text(
                    "section.ancient_event_debug.description",
                    "Debug tools for Ancient event pages."));

                section.AddToggle(
                    "enable_ancient_event_debug_reroll",
                    Text("toggle.enable_ancient_event_debug_reroll.label", "Enable Ancient event debug reroll"),
                    EnableAncientEventDebugRerollBinding,
                    Text(
                        "toggle.enable_ancient_event_debug_reroll.description",
                        "When enabled, Ancient event pages show a debug Reroll button that regenerates the current option set."));
            });

            page.AddSection("relic_pickup_debug", section =>
            {
                section.WithTitle(Text("section.relic_pickup_debug.title", "Relic pickup prediction"));
                section.WithDescription(Text(
                    "section.relic_pickup_debug.description",
                    "Debug tools for relic pickup prediction."));

                section.AddButton(
                    "offer_predicted_non_ancient_relics",
                    Text("button.offer_predicted_non_ancient_relics.label", "Offer predicted non-Ancient relics"),
                    Text(
                        "button.offer_predicted_non_ancient_relics.text",
                        "Offer"),
                    Debug.RelicPickupDebugRewards.OfferPredictedNonAncientRelics,
                    ModSettingsButtonTone.Danger,
                    Text(
                        "button.offer_predicted_non_ancient_relics.description",
                        "Only works during a run. Opens a reward screen with the non-Ancient relics covered by pickup prediction. This can change game content."));

                section.AddButton(
                    "open_predicted_treasure_room",
                    Text("button.open_predicted_treasure_room.label", "Open predicted treasure room"),
                    Text(
                        "button.open_predicted_treasure_room.text",
                        "Open"),
                    Debug.RelicPickupDebugRewards.OpenPredictedTreasureRoom,
                    ModSettingsButtonTone.Danger,
                    Text(
                        "button.open_predicted_treasure_room.description",
                        "Only works during a run. Opens a treasure room whose relic target is randomly chosen from War Paint and Whetstone. This can change game content."));

                section.AddButton(
                    "open_relic_trader_pickup_test",
                    Text("button.open_relic_trader_pickup_test.label", "Open Relic Trader pickup test"),
                    Text(
                        "button.open_relic_trader_pickup_test.text",
                        "Open"),
                    Debug.RelicPickupDebugRewards.OpenRelicTraderPickupTest,
                    ModSettingsButtonTone.Danger,
                    Text(
                        "button.open_relic_trader_pickup_test.description",
                        "Only works during a run. Gives you a Circlet, then opens a Relic Trader event with Circlet trades for War Paint and Whetstone. This can change game content."));
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

    private static ModSettingsText Text(string key, string fallback)
    {
        return ModSettingsText.I18N(SettingsLocalization, key, fallback);
    }

    private static NetGameType GetCurrentNetGameType()
    {
        return RunManager.Instance.IsInProgress
            ? RunManager.Instance.NetService.Type
            : NetGameType.None;
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
